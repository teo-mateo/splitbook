using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SplitBook.Api.Domain;
using SplitBook.Api.Features.Auth.Register;
using SplitBook.Api.Features.Groups.GetGroup;
using SplitBook.Api.Features.Groups.ListMyGroups;
using SplitBook.Api.Infrastructure.Persistence;
using SplitBook.Api.Tests.Infrastructure;
using Xunit;

namespace SplitBook.Api.Tests.Features.Groups.GetGroup;

public class GetGroupEndpointTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public GetGroupEndpointTests(AppFactory factory)
    {
        _factory = factory;
    }

    private async Task<string> GetAuthTokenAsync(string email, string password)
    {
        var client = _factory.CreateClient();

        var registerRequest = new { email, displayName = "TestUser", password };
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        var loginRequest = new { email, password };
        var loginResponse = await client.PostAsJsonAsync("/auth/login", loginRequest);
        var loginBody = await loginResponse.Content.ReadAsStringAsync();
        var loginDoc = System.Text.Json.JsonDocument.Parse(loginBody);
        var accessToken = loginDoc.RootElement.GetProperty("accessToken").GetString();

        return accessToken!;
    }

    private async Task<HttpClient> CreateAuthClientAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var token = await GetAuthTokenAsync(email, password);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task GetGroup_MemberReadsOwnGroup_Returns200WithGroupDetail()
    {
        // Arrange — register, log in, and create a group (creator auto-added as member)
        var client = await CreateAuthClientAsync("detail@example.com", "Detail123!");
        var createRequest = new { name = "Lisbon Trip", currency = "EUR" };
        var createResponse = await client.PostAsJsonAsync("/groups", createRequest);
        createResponse.EnsureSuccessStatusCode();

        var createBody = await createResponse.Content.ReadFromJsonAsync<GroupDto>();
        createBody.Should().NotBeNull();
        var groupId = createBody!.Id;

        // Act
        var response = await client.GetAsync($"/groups/{groupId}");
        var body = await response.Content.ReadFromJsonAsync<GroupDetailDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Id.Should().Be(groupId);
        body.Name.Should().Be("Lisbon Trip");
        body.Currency.Should().Be("EUR");
        body.Members.Should().NotBeEmpty();
        body.Members.Should().HaveCount(1);
        body.Members[0].UserId.Should().NotBe(Guid.Empty);
        body.Members[0].DisplayName.Should().Be("TestUser");
    }

    [Fact]
    public async Task GetGroup_CreatorAppearsInMembers_MatchesCreatorUserId()
    {
        // Arrange — register and capture the creator's user ID
        var client = _factory.CreateClient();
        var email = "creator_member_check@example.com";
        var password = "CreatorCheck123!";

        var registerRequest = new { email, displayName = "CreatorUser", password };
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        var registerBody = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
        registerBody.Should().NotBeNull();
        var creatorUserId = registerBody!.Id;

        // Log in to get auth token
        var loginRequest = new { email, password };
        var loginResponse = await client.PostAsJsonAsync("/auth/login", loginRequest);
        var loginBody = await loginResponse.Content.ReadAsStringAsync();
        var loginDoc = System.Text.Json.JsonDocument.Parse(loginBody);
        var accessToken = loginDoc.RootElement.GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken!);

        // Create a group (creator auto-added as member)
        var createRequest = new { name = "Creator Check Group", currency = "EUR" };
        var createResponse = await client.PostAsJsonAsync("/groups", createRequest);
        createResponse.EnsureSuccessStatusCode();

        var createBody = await createResponse.Content.ReadFromJsonAsync<GroupDto>();
        createBody.Should().NotBeNull();
        var groupId = createBody!.Id;

        // Act
        var response = await client.GetAsync($"/groups/{groupId}");
        var body = await response.Content.ReadFromJsonAsync<GroupDetailDto>();

        // Assert — members array contains the creator's user ID
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Members.Should().ContainSingle(m => m.UserId == creatorUserId);
    }

    [Fact]
    public async Task GetGroup_NonMemberGets404_Not403()
    {
        // Arrange — user A creates a group (auto-added as member)
        var clientA = await CreateAuthClientAsync("group_owner_404@example.com", "Owner404!");
        var createRequest = new { name = "Private Group", currency = "EUR" };
        var createResponse = await clientA.PostAsJsonAsync("/groups", createRequest);
        createResponse.EnsureSuccessStatusCode();

        var createBody = await createResponse.Content.ReadFromJsonAsync<GroupDto>();
        createBody.Should().NotBeNull();
        var groupId = createBody!.Id;

        // User B is a separate user — NOT a member of the group
        var clientB = await CreateAuthClientAsync("stranger_404@example.com", "Stranger404!");

        // Act — user B tries to read the group
        var response = await clientB.GetAsync($"/groups/{groupId}");

        // Assert — must be 404 (not 403) to avoid leaking group existence
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a non-member should receive 404, not 403, to avoid leaking group existence");
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden,
            "403 would leak the fact that the group exists");
    }

    [Fact]
    public async Task GetGroup_NonExistentGroup_Returns404()
    {
        // Arrange — authenticated user, but no group created
        var client = await CreateAuthClientAsync("nonexistent_group@example.com", "NonExistent123!");
        var nonExistentGroupId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/groups/{nonExistentGroupId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "requesting a group that does not exist should return 404");
    }

    [Fact]
    public async Task GetGroup_UnauthenticatedRequest_Returns401()
    {
        // Arrange — no auth token at all
        var client = _factory.CreateClient();
        var anyGroupId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/groups/{anyGroupId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "an unauthenticated request should receive 401");
    }

    [Fact]
    public async Task GetGroup_RemovedMemberExcludedFromMembersArray()
    {
        // Arrange — user A creates a group (auto-added as member)
        var clientA = await CreateAuthClientAsync("removed_member_a@example.com", "RemovedA123!");

        var createRequest = new { name = "Removal Test Group", currency = "EUR" };
        var createResponse = await clientA.PostAsJsonAsync("/groups", createRequest);
        createResponse.EnsureSuccessStatusCode();

        var createBody = await createResponse.Content.ReadFromJsonAsync<GroupDto>();
        createBody.Should().NotBeNull();
        var groupId = createBody!.Id;

        // Register user B separately
        var clientB = _factory.CreateClient();
        var emailB = "removed_member_b@example.com";
        var passwordB = "RemovedB123!";

        var registerRequest = new { email = emailB, displayName = "RemovedUser", password = passwordB };
        var registerResponse = await clientB.PostAsJsonAsync("/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        var registerBody = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
        registerBody.Should().NotBeNull();
        var userIdB = registerBody!.Id;

        // Insert a Membership row directly via DbContext, then mark it as removed
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Memberships.Add(new Membership
            {
                GroupId = groupId,
                UserId = userIdB,
                JoinedAt = DateTimeOffset.UtcNow,
                RemovedAt = DateTimeOffset.UtcNow, // removed
            });
            await db.SaveChangesAsync();
        }

        // Act — user A reads the group detail
        var response = await clientA.GetAsync($"/groups/{groupId}");
        var body = await response.Content.ReadFromJsonAsync<GroupDetailDto>();

        // Assert — removed member B should NOT appear in the members array
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        body.Should().NotBeNull();
        body!.Members.Should().NotContain(m => m.UserId == userIdB,
            "a removed member (RemovedAt is set) should not appear in the members array");
    }

    [Fact]
    public async Task GetGroup_NonExistentGroup_ReturnsProblemJson()
    {
        // Arrange — authenticated user, non-existent group
        var client = await CreateAuthClientAsync("problem_json@example.com", "ProblemJson123!");
        var nonExistentGroupId = Guid.NewGuid();

        // Act
        var response = await client.GetAsync($"/groups/{nonExistentGroupId}");
        var body = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();

        // Assert — RFC 7807 Problem+JSON shape
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        body.Should().NotBeNull();
        body!.Type.Should().NotBeNullOrEmpty("Problem+JSON must include a 'type' field");
        body.Title.Should().NotBeNullOrEmpty("Problem+JSON must include a 'title' field");
        body.Status.Should().Be(404, "Problem+JSON 'status' must match the HTTP status code");
    }
}
