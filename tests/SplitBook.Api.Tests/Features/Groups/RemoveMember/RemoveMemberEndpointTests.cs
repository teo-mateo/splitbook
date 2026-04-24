using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SplitBook.Api.Features.Auth.Login;
using SplitBook.Api.Features.Auth.Register;
using SplitBook.Api.Features.Groups.AddMember;
using SplitBook.Api.Features.Groups.CreateGroup;
using SplitBook.Api.Features.Groups.GetGroup;
using SplitBook.Api.Features.Groups.ListMyGroups;
using SplitBook.Api.Tests.Infrastructure;
using Xunit;

namespace SplitBook.Api.Tests.Features.Groups.RemoveMember;

public class RemoveMemberEndpointTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public RemoveMemberEndpointTests(AppFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Helper: log in an existing user and return the bearer token.
    /// </summary>
    private async Task<string> LoginAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var loginRequest = new LoginRequest(email, password);
        var loginResponse = await client.PostAsJsonAsync("/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();
        return loginResult!.AccessToken;
    }

    /// <summary>
    /// Helper: create an HttpClient configured with a bearer token (user must already be registered).
    /// </summary>
    private async Task<HttpClient> CreateAuthClientAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(email, password);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task RemoveMember_HappyPath_Returns204AndMemberRemoved()
    {
        // Arrange — register two users
        const string passwordA = "UserAPass123!";
        const string passwordB = "UserBPass123!";
        const string emailA = "remove-test-a@example.com";
        const string emailB = "remove-test-b@example.com";

        var unauthClient = _factory.CreateClient();
        var regA = await unauthClient.PostAsJsonAsync("/auth/register", new RegisterRequest(emailA, "UserA", passwordA));
        regA.EnsureSuccessStatusCode();
        var regB = await unauthClient.PostAsJsonAsync("/auth/register", new RegisterRequest(emailB, "UserB", passwordB));
        regB.EnsureSuccessStatusCode();

        // Log in as user A and create a group
        var clientA = await CreateAuthClientAsync(emailA, passwordA);
        var groupResponse = await clientA.PostAsJsonAsync("/groups", new CreateGroupRequest("Test Group", "EUR"));
        var groupDto = await groupResponse.Content.ReadFromJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B to the group
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", new AddMemberRequest(emailB));
        addMemberResponse.EnsureSuccessStatusCode();

        // Get group detail to find user B's userId
        var detailResponse = await clientA.GetAsync($"/groups/{groupId}");
        var groupDetail = await detailResponse.Content.ReadFromJsonAsync<GroupDetailDto>();
        var userBId = groupDetail!.Members.Single(m => m.DisplayName == "UserB").UserId;

        // Act — remove user B
        var removeResponse = await clientA.DeleteAsync($"/groups/{groupId}/members/{userBId}");

        // Assert — 204 No Content
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert — user B is no longer in the group (only user A remains)
        var afterDetailResponse = await clientA.GetAsync($"/groups/{groupId}");
        var afterDetail = await afterDetailResponse.Content.ReadFromJsonAsync<GroupDetailDto>();
        afterDetail!.Members.Should().HaveCount(1);
        afterDetail.Members.Single().UserId.Should().NotBe(userBId);
    }

    [Fact]
    public async Task RemoveMember_CallerNotMember_Returns404()
    {
        // Arrange — register user A (creates group) and user C (outsider)
        const string passwordA = "CallerNotA123!";
        const string passwordC = "CallerNotC123!";
        const string emailA = "caller-not-a@example.com";
        const string emailC = "caller-not-c@example.com";

        var unauthClient = _factory.CreateClient();
        await unauthClient.PostAsJsonAsync("/auth/register", new RegisterRequest(emailA, "UserA", passwordA));
        await unauthClient.PostAsJsonAsync("/auth/register", new RegisterRequest(emailC, "UserC", passwordC));

        // User A creates a group
        var clientA = await CreateAuthClientAsync(emailA, passwordA);
        var groupResponse = await clientA.PostAsJsonAsync("/groups", new CreateGroupRequest("CallerNot Group", "EUR"));
        var groupDto = await groupResponse.Content.ReadFromJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Act — user C (not a member) tries to remove a member
        var clientC = await CreateAuthClientAsync(emailC, passwordC);
        var removeResponse = await clientC.DeleteAsync($"/groups/{groupId}/members/{Guid.NewGuid()}");

        // Assert — 404
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveMember_GroupDoesNotExist_Returns404()
    {
        // Arrange — register and log in user A
        const string passwordA = "GroupNotA123!";
        const string emailA = "group-not-a@example.com";

        var unauthClient = _factory.CreateClient();
        await unauthClient.PostAsJsonAsync("/auth/register", new RegisterRequest(emailA, "UserA", passwordA));

        var clientA = await CreateAuthClientAsync(emailA, passwordA);

        // Act — DELETE with a fake group GUID
        var fakeGroupId = Guid.NewGuid();
        var removeResponse = await clientA.DeleteAsync($"/groups/{fakeGroupId}/members/{Guid.NewGuid()}");

        // Assert — 404
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveMember_UserNotInGroup_Returns404()
    {
        // Arrange — register A, B, C. A creates group, adds B.
        const string passwordA = "UserNotA123!";
        const string passwordB = "UserNotB123!";
        const string passwordC = "UserNotC123!";
        const string emailA = "user-not-a@example.com";
        const string emailB = "user-not-b@example.com";
        const string emailC = "user-not-c@example.com";

        var unauthClient = _factory.CreateClient();
        await unauthClient.PostAsJsonAsync("/auth/register", new RegisterRequest(emailA, "UserA", passwordA));
        await unauthClient.PostAsJsonAsync("/auth/register", new RegisterRequest(emailB, "UserB", passwordB));
        await unauthClient.PostAsJsonAsync("/auth/register", new RegisterRequest(emailC, "UserC", passwordC));

        var clientA = await CreateAuthClientAsync(emailA, passwordA);
        var groupResponse = await clientA.PostAsJsonAsync("/groups", new CreateGroupRequest("UserNot Group", "EUR"));
        var groupDto = await groupResponse.Content.ReadFromJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add B to the group
        await clientA.PostAsJsonAsync($"/groups/{groupId}/members", new AddMemberRequest(emailB));

        // Get group detail to find user C's userId (C isn't in the group, so register response)
        var regC = await unauthClient.PostAsJsonAsync("/auth/register", new RegisterRequest(emailC + "2", "UserC2", passwordC));
        // Actually, C is already registered. Get C's ID from a separate group or from register response.
        // Simpler: parse C's ID from register response above — but we already registered C.
        // Let's get C's ID by having C create a throwaway group and reading the creator ID.
        var clientC = await CreateAuthClientAsync(emailC, passwordC);
        var throwawayGroup = await clientC.PostAsJsonAsync("/groups", new CreateGroupRequest("Throwaway", "USD"));
        var throwawayDto = await throwawayGroup.Content.ReadFromJsonAsync<GroupDto>();
        // Actually, GroupDto doesn't have creator info. Get it from group detail.
        var throwawayDetail = await clientC.GetAsync($"/groups/{throwawayDto!.Id}");
        var throwawayDetailDto = await throwawayDetail.Content.ReadFromJsonAsync<GroupDetailDto>();
        var userCId = throwawayDetailDto!.Members.Single(m => m.DisplayName == "UserC").UserId;

        // Act — user A tries to remove user C (not a member of the group)
        var removeResponse = await clientA.DeleteAsync($"/groups/{groupId}/members/{userCId}");

        // Assert — 404
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemoveMember_WithoutToken_Returns401()
    {
        // Arrange — no auth header
        var unauthClient = _factory.CreateClient();

        // Act — DELETE without authentication
        var fakeGroupId = Guid.NewGuid();
        var fakeUserId = Guid.NewGuid();
        var removeResponse = await unauthClient.DeleteAsync($"/groups/{fakeGroupId}/members/{fakeUserId}");

        // Assert — 401
        removeResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RemoveMember_RemoveSelf_Returns204()
    {
        // Arrange — register A and B. A creates group, adds B.
        const string passwordA = "RemoveSelfA123!";
        const string passwordB = "RemoveSelfB123!";
        const string emailA = "remove-self-a@example.com";
        const string emailB = "remove-self-b@example.com";

        var unauthClient = _factory.CreateClient();
        await unauthClient.PostAsJsonAsync("/auth/register", new RegisterRequest(emailA, "UserA", passwordA));
        await unauthClient.PostAsJsonAsync("/auth/register", new RegisterRequest(emailB, "UserB", passwordB));

        var clientA = await CreateAuthClientAsync(emailA, passwordA);
        var groupResponse = await clientA.PostAsJsonAsync("/groups", new CreateGroupRequest("RemoveSelf Group", "EUR"));
        var groupDto = await groupResponse.Content.ReadFromJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add B to the group
        await clientA.PostAsJsonAsync($"/groups/{groupId}/members", new AddMemberRequest(emailB));

        // Get group detail to find user IDs
        var detailResponse = await clientA.GetAsync($"/groups/{groupId}");
        var groupDetail = await detailResponse.Content.ReadFromJsonAsync<GroupDetailDto>();
        var userAId = groupDetail!.Members.Single(m => m.DisplayName == "UserA").UserId;
        var userBId = groupDetail.Members.Single(m => m.DisplayName == "UserB").UserId;

        // Act — B logs in and removes themselves
        var clientB = await CreateAuthClientAsync(emailB, passwordB);
        var removeResponse = await clientB.DeleteAsync($"/groups/{groupId}/members/{userBId}");

        // Assert — 204
        removeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert — B no longer in members, A still there
        var afterDetailResponse = await clientA.GetAsync($"/groups/{groupId}");
        afterDetailResponse.EnsureSuccessStatusCode();
        var afterDetail = await afterDetailResponse.Content.ReadFromJsonAsync<GroupDetailDto>();
        afterDetail.Should().NotBeNull();
        afterDetail!.Members.Should().HaveCount(1);
        afterDetail.Members.Single().UserId.Should().Be(userAId);
    }
}
