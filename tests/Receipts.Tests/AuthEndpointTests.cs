using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Receipts.Tests;

public class AuthEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuthEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Register_WithValidCredentials_Returns201Created()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/register",
            new { email = "juan@example.com", password = "P@ssw0rd123" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns409Conflict()
    {
        var client = _factory.CreateClient();
        var payload = new { email = "dupe@example.com", password = "P@ssw0rd123" };

        await client.PostAsJsonAsync("/auth/register", payload);              // first time: created
        var second = await client.PostAsJsonAsync("/auth/register", payload); // second time: conflict

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectCredentials_Returns200WithToken()
    {
        var client = _factory.CreateClient();
        var creds = new { email = "login@example.com", password = "P@ssw0rd123" };
        await client.PostAsJsonAsync("/auth/register", creds);

        var response = await client.PostAsJsonAsync("/auth/login", creds);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/auth/register",
            new { email = "wrongpw@example.com", password = "correct-password" });

        var response = await client.PostAsJsonAsync("/auth/login",
            new { email = "wrongpw@example.com", password = "WRONG-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login",
            new { email = "nobody@example.com", password = "whatever" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private record LoginResponse(string Token);
}
