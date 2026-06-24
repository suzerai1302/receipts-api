using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Receipts.Tests;

public class GroupEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public GroupEndpointTests(TestWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateGroup_WithoutToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/groups", new { name = "Barkada Dinner" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateGroup_WithToken_Returns201WithGroupAndCreatorAsMember()
    {
        var client = await AuthenticatedClientAsync("creator@example.com");

        var response = await client.PostAsJsonAsync("/groups", new { name = "Barkada Dinner" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var group = await response.Content.ReadFromJsonAsync<GroupResponse>();
        Assert.NotEqual(Guid.Empty, group!.Id);
        Assert.Equal("Barkada Dinner", group.Name);
        Assert.Contains("creator@example.com", group.Members);
    }

    [Fact]
    public async Task AddMember_AddsEmailToGroup()
    {
        var client = await AuthenticatedClientAsync("owner@example.com");
        var created = await (await client.PostAsJsonAsync("/groups", new { name = "Trip" }))
            .Content.ReadFromJsonAsync<GroupResponse>();

        var response = await client.PostAsJsonAsync(
            $"/groups/{created!.Id}/members", new { email = "friend@example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var group = await response.Content.ReadFromJsonAsync<GroupResponse>();
        Assert.Contains("friend@example.com", group!.Members);
    }

    [Fact]
    public async Task AddMembersBatch_AddsAllEmailsAndSkipsDuplicates()
    {
        var client = await AuthenticatedClientAsync("host@example.com");
        var created = await (await client.PostAsJsonAsync("/groups", new { name = "Trip" }))
            .Content.ReadFromJsonAsync<GroupResponse>();

        var response = await client.PostAsJsonAsync(
            $"/groups/{created!.Id}/members/batch",
            new { emails = new[] { "a@example.com", "b@example.com", "host@example.com" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var group = await response.Content.ReadFromJsonAsync<GroupResponse>();
        Assert.Contains("a@example.com", group!.Members);
        Assert.Contains("b@example.com", group.Members);
        // host was already a member (creator) — not duplicated.
        Assert.Equal(3, group.Members.Count);
    }

    [Fact]
    public async Task AddExpense_ThenGetSettlement_ReturnsWhoOwesWhom()
    {
        var client = await AuthenticatedClientAsync("payer@example.com");
        var group = await (await client.PostAsJsonAsync("/groups", new { name = "Trip" }))
            .Content.ReadFromJsonAsync<GroupResponse>();

        await client.PostAsJsonAsync($"/groups/{group!.Id}/members", new { email = "b@example.com" });
        await client.PostAsJsonAsync($"/groups/{group.Id}/members", new { email = "c@example.com" });

        var addExpense = await client.PostAsJsonAsync($"/groups/{group.Id}/expenses", new
        {
            payerId = "payer@example.com",
            amount = 90m,
            participantIds = new[] { "payer@example.com", "b@example.com", "c@example.com" }
        });
        Assert.Equal(HttpStatusCode.Created, addExpense.StatusCode);

        var response = await client.GetAsync($"/groups/{group.Id}/settlement");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settlements = await response.Content.ReadFromJsonAsync<List<SettlementDto>>();
        Assert.Equal(2, settlements!.Count);
        Assert.Contains(settlements, s => s.DebtorId == "b@example.com" && s.CreditorId == "payer@example.com" && s.Amount == 30m);
        Assert.Contains(settlements, s => s.DebtorId == "c@example.com" && s.CreditorId == "payer@example.com" && s.Amount == 30m);
    }

    // Registers a unique user, logs in, and returns an authenticated client.
    private async Task<HttpClient> AuthenticatedClientAsync(string email)
    {
        var client = _factory.CreateClient();
        var creds = new { email, password = "P@ssw0rd123" };
        await client.PostAsJsonAsync("/auth/register", creds);

        var login = await client.PostAsJsonAsync("/auth/login", creds);
        var body = await login.Content.ReadFromJsonAsync<TokenBody>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Token);
        return client;
    }

    private record TokenBody(string Token);
    private record GroupResponse(Guid Id, string Name, List<string> Members);
    private record SettlementDto(string DebtorId, string CreditorId, decimal Amount);
}
