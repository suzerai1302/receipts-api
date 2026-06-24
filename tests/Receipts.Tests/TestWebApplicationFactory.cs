using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Receipts.Infrastructure;

namespace Receipts.Tests;

// Boots the real app in the "Testing" environment (where Program skips the
// Postgres registration) and supplies a SQLite in-memory database instead,
// so the EF Core code paths are exercised end-to-end in tests.
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        _connection.Open(); // keep the in-memory database alive for the factory's lifetime

        builder.ConfigureServices(services =>
        {
            services.AddDbContext<ReceiptsDbContext>(options => options.UseSqlite(_connection));

            using var scope = services.BuildServiceProvider().CreateScope();
            scope.ServiceProvider.GetRequiredService<ReceiptsDbContext>().Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
