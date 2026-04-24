using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SplitBook.Api.Features.Auth.Login;
using SplitBook.Api.Features.Auth.Register;
using SplitBook.Api.Features.Groups.CreateGroup;
using SplitBook.Api.Features.Groups.ListMyGroups;
using SplitBook.Api.Features.Groups.GetGroup;
using SplitBook.Api.Tests.Infrastructure;
using Xunit;

namespace SplitBook.Api.Tests.Features.Groups.ArchiveGroup;

public class ArchiveGroupEndpointTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public ArchiveGroupEndpointTests(AppFactory factory)
    {
        _factory = factory;
    }

    private async Task<string> LoginAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var loginRequest = new LoginRequest(email, password);
        var loginResponse = await client.PostAsJsonAsync("/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        return loginResult!.AccessToken;
    }

    private async Task<HttpClient> CreateAuthClientAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(email, password);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task ArchiveGroup_HappyPath_Returns204AndSetsArchivedAt()
    {
        // Arrange — register user, log in, create a group
        const string password = "ArchivePass123!";
        const string email = "archive-test@example.com";

        var unauthClient = _factory.CreateClient();
        await unauthClient.PostAsJsonAsync("/auth/register", new RegisterRequest(email, "ArchiveUser", password));

        var client = await CreateAuthClientAsync(email, password);
        var groupResponse = await client.PostAsJsonAsync("/groups", new CreateGroupRequest("Archive Me", "EUR"));
        var groupDto = await groupResponse.Content.ReadFromJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Act — archive the group
        var archiveResponse = await client.PostAsync($"/groups/{groupId}/archive", null);

        // Assert — 204 No Content
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert — ArchivedAt is now set (non-null) via GET /groups/{id}
        var detailResponse = await client.GetAsync($"/groups/{groupId}");
        detailResponse.EnsureSuccessStatusCode();
        var groupDetail = await detailResponse.Content.ReadFromJsonAsync<GroupDetailDto>();
        groupDetail.Should().NotBeNull();
        groupDetail!.ArchivedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ArchiveGroup_WithoutToken_Returns401()
    {
        // Arrange — unauthenticated client (no Bearer token)
        var client = _factory.CreateClient();

        // Act — POST to archive endpoint without auth
        var fakeGroupId = Guid.NewGuid();
        var response = await client.PostAsync($"/groups/{fakeGroupId}/archive", null);

        // Assert — 401 Unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ArchiveGroup_GroupDoesNotExist_Returns404()
    {
        // Arrange — register user, log in (authenticated, but no group exists for fakeGuid)
        const string password = "Archive404Pass!";
        const string email = "archive-404-test@example.com";

        var unauthClient = _factory.CreateClient();
        await unauthClient.PostAsJsonAsync("/auth/register", new RegisterRequest(email, "Archive404User", password));

        var client = await CreateAuthClientAsync(email, password);
        var fakeGroupId = Guid.NewGuid();

        // Act — POST to archive endpoint with a non-existent group ID
        var response = await client.PostAsync($"/groups/{fakeGroupId}/archive", null);

        // Assert — 404 Not Found (not 403)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ArchiveGroup_CallerNotMember_Returns404()
    {
        // Arrange — register two users: A (group creator) and C (not a member)
        const string password = "NotMemberPass!";
        const string emailA = "archive-not-member-a@example.com";
        const string emailC = "archive-not-member-c@example.com";

        var unauthClient = _factory.CreateClient();
        await unauthClient.PostAsJsonAsync("/auth/register", new RegisterRequest(emailA, "UserA", password));
        await unauthClient.PostAsJsonAsync("/auth/register", new RegisterRequest(emailC, "UserC", password));

        // User A creates a group (A becomes the only member)
        var clientA = await CreateAuthClientAsync(emailA, password);
        var groupResponse = await clientA.PostAsJsonAsync("/groups", new CreateGroupRequest("Not My Group", "USD"));
        var groupDto = await groupResponse.Content.ReadFromJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // User C (not a member) tries to archive the group
        var clientC = await CreateAuthClientAsync(emailC, password);

        // Act
        var response = await clientC.PostAsync($"/groups/{groupId}/archive", null);

        // Assert — 404 Not Found (not 403)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ArchiveGroup_AlreadyArchived_Returns204()
    {
        // Arrange — register user, log in, create a group
        const string password = "ArchiveIdemPass123!";
        const string email = "archive-idempotent@example.com";

        var unauthClient = _factory.CreateClient();
        await unauthClient.PostAsJsonAsync("/auth/register", new RegisterRequest(email, "ArchiveIdemUser", password));

        var client = await CreateAuthClientAsync(email, password);
        var groupResponse = await client.PostAsJsonAsync("/groups", new CreateGroupRequest("Archive Twice", "GBP"));
        var groupDto = await groupResponse.Content.ReadFromJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Archive the group once
        var firstArchiveResponse = await client.PostAsync($"/groups/{groupId}/archive", null);
        firstArchiveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act — archive the already-archived group again
        var secondArchiveResponse = await client.PostAsync($"/groups/{groupId}/archive", null);

        // Assert — 204 No Content (idempotent)
        secondArchiveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
