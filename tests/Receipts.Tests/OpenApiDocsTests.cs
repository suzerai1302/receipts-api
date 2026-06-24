using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Receipts.Tests;

public class OpenApiDocsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public OpenApiDocsTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task OpenApiSpec_DeclaresBearerSecurityScheme_AndSecuresAuthedEndpoints()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        // The Bearer scheme is documented (this is what renders the Authorize box).
        var scheme = root.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", scheme.GetProperty("type").GetString());
        Assert.Equal("bearer", scheme.GetProperty("scheme").GetString());

        // An authed endpoint carries a security requirement...
        var groupsPost = root.GetProperty("paths").GetProperty("/groups").GetProperty("post");
        Assert.True(groupsPost.TryGetProperty("security", out _));

        // ...while a public endpoint does not.
        var login = root.GetProperty("paths").GetProperty("/auth/login").GetProperty("post");
        Assert.False(login.TryGetProperty("security", out _));
    }
}
