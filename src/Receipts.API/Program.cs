using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Receipts.API;
using Receipts.Core;
using Receipts.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Honor the platform-assigned port (Render/Railway set PORT); ignored locally and under tests.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    // Document the JWT scheme + mark authed endpoints, so Scalar shows an Authorize box.
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
    options.AddOperationTransformer<AuthorizationOperationTransformer>();
});
if (!builder.Environment.IsEnvironment("Testing"))
{
    // Render/Heroku expose a postgres:// URL; convert it to an Npgsql connection string.
    var pgConn = builder.Configuration.GetConnectionString("Postgres");
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (!string.IsNullOrEmpty(databaseUrl))
    {
        var uri = new Uri(databaseUrl);
        var creds = uri.UserInfo.Split(':', 2);
        var dbPort = uri.Port == -1 ? 5432 : uri.Port;
        // Credentials in the URL are percent-encoded (Neon/Render passwords can contain special chars).
        var user = Uri.UnescapeDataString(creds[0]);
        var pass = creds.Length > 1 ? Uri.UnescapeDataString(creds[1]) : "";
        pgConn = $"Host={uri.Host};Port={dbPort};Database={uri.AbsolutePath.TrimStart('/')};" +
                 $"Username={user};Password={pass};SSL Mode=Require;Trust Server Certificate=true";
    }

    builder.Services.AddDbContext<ReceiptsDbContext>(options => options.UseNpgsql(pgConn));
}
builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IGroupRepository, EfGroupRepository>();
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Render (and most PaaS) terminate TLS at a proxy and forward plain HTTP with an
// X-Forwarded-Proto header. Honor it so Request.Scheme is "https" — otherwise the
// generated OpenAPI server URL is http:// and Scalar's browser calls get blocked as
// mixed content. KnownProxies/Networks are cleared because the proxy isn't loopback.
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedOptions.KnownIPNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

// Apply pending migrations on startup (skipped under tests, which use SQLite).
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<ReceiptsDbContext>().Database.Migrate();
}

// OpenAPI spec + Scalar interactive docs, live in all environments for the demo.
app.MapOpenApi();
app.MapScalarApiReference();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok());

app.MapPost("/auth/register", async (RegisterRequest request, IUserRepository users, IPasswordHasher hasher) =>
{
    if (await users.GetByEmailAsync(request.Email) is not null)
        return Results.Conflict(new { error = "Email already registered" });

    await users.AddAsync(new User { Email = request.Email, PasswordHash = hasher.Hash(request.Password) });
    return Results.Created($"/users/{request.Email}", null);
});

app.MapPost("/auth/login", async (LoginRequest request, IUserRepository users, IPasswordHasher hasher, ITokenService tokens) =>
{
    var user = await users.GetByEmailAsync(request.Email);
    if (user is null || !hasher.Verify(request.Password, user.PasswordHash))
        return Results.Unauthorized();

    return Results.Ok(new { token = tokens.CreateToken(user) });
});

app.MapPost("/groups", async (CreateGroupRequest request, IGroupRepository groups, ClaimsPrincipal principal) =>
{
    var creatorEmail = principal.FindFirstValue(ClaimTypes.Email) ?? principal.FindFirstValue("email")!;
    var group = new Group { Name = request.Name };
    group.MemberEmails.Add(creatorEmail);
    await groups.AddAsync(group);

    return Results.Created($"/groups/{group.Id}",
        new GroupResponse(group.Id, group.Name, group.MemberEmails));
}).RequireAuthorization();

app.MapPost("/groups/{id:guid}/members", async (Guid id, AddMemberRequest request, IGroupRepository groups) =>
{
    var group = await groups.GetByIdAsync(id);
    if (group is null) return Results.NotFound();

    if (!group.MemberEmails.Contains(request.Email, StringComparer.OrdinalIgnoreCase))
        group.MemberEmails.Add(request.Email);

    await groups.UpdateAsync(group);
    return Results.Ok(new GroupResponse(group.Id, group.Name, group.MemberEmails));
}).RequireAuthorization();

app.MapPost("/groups/{id:guid}/members/batch", async (Guid id, AddMembersRequest request, IGroupRepository groups) =>
{
    var group = await groups.GetByIdAsync(id);
    if (group is null) return Results.NotFound();

    foreach (var email in request.Emails)
        if (!string.IsNullOrWhiteSpace(email) &&
            !group.MemberEmails.Contains(email, StringComparer.OrdinalIgnoreCase))
            group.MemberEmails.Add(email);

    await groups.UpdateAsync(group);
    return Results.Ok(new GroupResponse(group.Id, group.Name, group.MemberEmails));
}).RequireAuthorization();

app.MapPost("/groups/{id:guid}/expenses", async (Guid id, AddExpenseRequest request, IGroupRepository groups) =>
{
    var group = await groups.GetByIdAsync(id);
    if (group is null) return Results.NotFound();

    group.Expenses.Add(new Expense(request.PayerId, request.Amount, request.ParticipantIds));
    await groups.UpdateAsync(group);
    return Results.Created($"/groups/{id}/expenses", null);
}).RequireAuthorization();

app.MapGet("/groups/{id:guid}/settlement", async (Guid id, IGroupRepository groups) =>
{
    var group = await groups.GetByIdAsync(id);
    if (group is null) return Results.NotFound();

    return Results.Ok(SettlementCalculator.Calculate(group.Expenses));
}).RequireAuthorization();

app.Run();

public record RegisterRequest(string Email, string Password);
public record LoginRequest(string Email, string Password);
public record CreateGroupRequest(string Name);
public record GroupResponse(Guid Id, string Name, List<string> Members);
public record AddMemberRequest(string Email);
public record AddMembersRequest(List<string> Emails);
public record AddExpenseRequest(string PayerId, decimal Amount, List<string> ParticipantIds);
