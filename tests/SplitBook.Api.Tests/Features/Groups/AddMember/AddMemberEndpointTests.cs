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

namespace SplitBook.Api.Tests.Features.Groups.AddMember;

public class AddMemberEndpointTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public AddMemberEndpointTests(AppFactory factory)
    {
        _factory = factory;
    }

    private async Task<string> GetAuthTokenAsync(string email, string password)
    {
        var client = _factory.CreateClient();

        var registerRequest = new RegisterRequest(email, "TestUser", password);
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();

        var loginRequest = new LoginRequest(email, password);
        var loginResponse = await client.PostAsJsonAsync("/auth/login", loginRequest);
        var loginResult = await loginResponse.ReadJsonAsync<LoginResponse>();

        return loginResult!.AccessToken;
    }

    private async Task<HttpClient> CreateAuthClientAsync(string email, string password)
    {
        var client = _factory.CreateClient();
        var token = await GetAuthTokenAsync(email, password);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task AddMember_ExistingUserByEmail_Returns204AndMemberAppearsInGroup()
    {
        // Arrange — register user A and user B
        var clientA = await CreateAuthClientAsync("addmember_a@example.com", "AddMemberA123!");

        // Register user B (separate client, no auth needed for register)
        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("userB@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();

        // User A creates a group (auto-added as member)
        var createGroupRequest = new CreateGroupRequest("Add Member Test Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        createGroupResponse.EnsureSuccessStatusCode();
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        groupDto.Should().NotBeNull();
        var groupId = groupDto!.Id;

        // Act — add user B to the group by email
        var addMemberRequest = new AddMemberRequest("userB@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);

        // Assert — 204 No Content
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert — GET /groups/{id} includes user B in members
        var groupDetailResponse = await clientA.GetAsync($"/groups/{groupId}");
        var groupDetail = await groupDetailResponse.ReadJsonAsync<GroupDetailDto>();

        groupDetailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        groupDetail.Should().NotBeNull();
        groupDetail!.Members.Should().HaveCount(2, "group should have both user A (creator) and user B (added)");
        groupDetail.Members.Should().Contain(m => m.DisplayName == "User B", "user B should appear in the members list");
    }

    [Fact]
    public async Task AddMember_CallerNotMember_Returns404()
    {
        // Arrange — register user A (group creator), user B (outsider), user C (target to add)
        var clientA = await CreateAuthClientAsync("notmember_a@example.com", "NotMemberA123!");
        var clientB = await CreateAuthClientAsync("notmember_b@example.com", "NotMemberB123!");

        // Register user C (the target to be added, so the email exists)
        var clientPlain = _factory.CreateClient();
        var registerC = new RegisterRequest("userc_notmember@example.com", "User C", "PassC123!");
        var registerCResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerC);
        registerCResponse.EnsureSuccessStatusCode();

        // User A creates a group (auto-added as member)
        var createGroupRequest = new CreateGroupRequest("Not Member Test Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        createGroupResponse.EnsureSuccessStatusCode();
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        groupDto.Should().NotBeNull();
        var groupId = groupDto!.Id;

        // Act — user B (NOT a member of the group) tries to add user C
        var addMemberRequest = new AddMemberRequest("userc_notmember@example.com");
        var addMemberResponse = await clientB.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);

        // Assert — 404 Not Found (not 403)
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddMember_GroupDoesNotExist_Returns404()
    {
        // Arrange — register and login user A
        var clientA = await CreateAuthClientAsync("group404_a@example.com", "Group404A123!");

        // Act — POST to a non-existent group id
        var fakeGroupId = Guid.NewGuid();
        var addMemberRequest = new AddMemberRequest("somebody@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{fakeGroupId}/members", addMemberRequest);

        // Assert — 404 Not Found
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddMember_EmailNotFound_Returns404()
    {
        // Arrange — register and login user A, create a group
        var clientA = await CreateAuthClientAsync("email404_a@example.com", "Email404A123!");

        var createGroupRequest = new CreateGroupRequest("Email Not Found Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        createGroupResponse.EnsureSuccessStatusCode();
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        groupDto.Should().NotBeNull();
        var groupId = groupDto!.Id;

        // Act — try to add a user that does not exist
        var addMemberRequest = new AddMemberRequest("nobody@nowhere.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);

        // Assert — 404 Not Found
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddMember_AlreadyMember_Returns409()
    {
        // Arrange — register user A and user B
        var clientA = await CreateAuthClientAsync("already_a@example.com", "AlreadyA123!");

        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("already_b@example.com", "Already B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();

        // User A creates a group
        var createGroupRequest = new CreateGroupRequest("Already Member Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        createGroupResponse.EnsureSuccessStatusCode();
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        groupDto.Should().NotBeNull();
        var groupId = groupDto!.Id;

        // Add user B to the group (first time — succeeds)
        var addMemberRequest = new AddMemberRequest("already_b@example.com");
        var firstAddResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        firstAddResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act — try to add user B again
        var secondAddResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);

        // Assert — 409 Conflict
        secondAddResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AddMember_WithoutToken_Returns401()
    {
        // Arrange — create a plain client with no auth header
        var clientPlain = _factory.CreateClient();

        // Act — POST without a bearer token
        var fakeGroupId = Guid.NewGuid();
        var addMemberRequest = new AddMemberRequest("somebody@example.com");
        var addMemberResponse = await clientPlain.PostAsJsonAsync($"/groups/{fakeGroupId}/members", addMemberRequest);

        // Assert — 401 Unauthorized
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddMember_InvalidEmail_Returns400()
    {
        // Arrange — register and login user A, create a group
        var clientA = await CreateAuthClientAsync("invalid_email_a@example.com", "InvalidEmailA123!");

        var createGroupRequest = new CreateGroupRequest("Invalid Email Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        createGroupResponse.EnsureSuccessStatusCode();
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        groupDto.Should().NotBeNull();
        var groupId = groupDto!.Id;

        // Act — POST with a malformed email
        var addMemberRequest = new AddMemberRequest("not-an-email");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);

        // Assert — 400 Bad Request with Problem+JSON body
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problemBody = await addMemberResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problemBody.Should().NotBeNull();
        problemBody!.Status.Should().Be(400);
    }

    [Fact]
    public async Task AddMember_ErrorResponses_ReturnProblemJsonShape()
    {
        // Arrange — caller not a member scenario
        var clientA = await CreateAuthClientAsync("problemjson_a@example.com", "ProblemJsonA123!");
        var clientB = await CreateAuthClientAsync("problemjson_b@example.com", "ProblemJsonB123!");

        var clientPlain = _factory.CreateClient();
        var registerC = new RegisterRequest("userc_problemjson@example.com", "User C", "PassC123!");
        var registerCResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerC);
        registerCResponse.EnsureSuccessStatusCode();

        var createGroupRequest = new CreateGroupRequest("Problem JSON Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Act — user B (not a member) tries to add user C
        var addMemberRequest = new AddMemberRequest("userc_problemjson@example.com");
        var addMemberResponse = await clientB.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);

        // Assert — Problem+JSON shape
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problemBody = await addMemberResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problemBody.Should().NotBeNull();
        problemBody!.Status.Should().Be(404);
        problemBody.Title.Should().NotBeNullOrEmpty();
    }
}
