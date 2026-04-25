using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using SplitBook.Api.Features.Auth.Login;
using SplitBook.Api.Features.Auth.Register;
using SplitBook.Api.Features.Groups.AddMember;
using SplitBook.Api.Features.Groups.CreateGroup;
using SplitBook.Api.Features.Groups.ListMyGroups;
using SplitBook.Api.Features.Settlements.RecordSettlement;
using SplitBook.Api.Tests.Infrastructure;
using Xunit;

namespace SplitBook.Api.Tests.Features.Settlements.ListSettlements;

public class ListSettlementsEndpointTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public ListSettlementsEndpointTests(AppFactory factory)
    {
        _factory = factory;
    }

    private async Task<(string Token, Guid UserId)> RegisterAndLoginAsync(string email, string password)
    {
        var client = _factory.CreateClient();

        var registerRequest = new RegisterRequest(email, "TestUser", password);
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();
        var registerResult = await registerResponse.ReadJsonAsync<RegisterResponse>();
        var userId = registerResult!.Id;

        var loginRequest = new LoginRequest(email, password);
        var loginResponse = await client.PostAsJsonAsync("/auth/login", loginRequest);
        var loginResult = await loginResponse.ReadJsonAsync<LoginResponse>();

        return (loginResult!.AccessToken, userId);
    }

    private async Task<(HttpClient Client, Guid UserId)> CreateAuthClientAsync(string email, string password)
    {
        var (token, userId) = await RegisterAndLoginAsync(email, password);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return (client, userId);
    }

    [Fact]
    public async Task ListSettlements_HappyPath_ReturnsAllSettlements()
    {
        // Arrange — register user A and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("list_settlements_a@example.com", "ListSettlementsA123!");

        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("list_settlements_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group
        var createGroupRequest = new CreateGroupRequest("List Settlements Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as member
        var addMemberRequest = new AddMemberRequest("list_settlements_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Record a settlement: B pays A 3000 EUR
        var settlementRequest = new RecordSettlementRequest(
            userIdB,
            userIdA,
            3000,
            "EUR",
            DateOnly.Parse("2024-06-15")
        );
        var settlementResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/settlements", settlementRequest);
        settlementResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act — GET /groups/{groupId}/settlements
        var response = await clientA.GetFromJsonAsync<List<SettlementDto>>($"/groups/{groupId}/settlements");

        // Assert — status 200 (GetFromJsonAsync throws on non-success, so reaching here means 200)
        response.Should().NotBeNull();

        // Assert — exactly one settlement
        response!.Count.Should().Be(1, "should return exactly one settlement");

        // Assert — settlement fields match
        var settlement = response[0];
        settlement.GroupId.Should().Be(groupId);
        settlement.FromUserId.Should().Be(userIdB);
        settlement.ToUserId.Should().Be(userIdA);
        settlement.AmountMinor.Should().Be(3000);
        settlement.Currency.Should().Be("EUR");
    }

    [Fact]
    public async Task ListSettlements_EmptyGroup_ReturnsEmptyArray()
    {
        // Arrange — user creates a group with no settlements
        var (client, _) = await CreateAuthClientAsync("empty_list_a@example.com", "EmptyListA123!");

        var createGroupRequest = new CreateGroupRequest("Empty Settlements Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Act — GET /groups/{groupId}/settlements
        var response = await client.GetFromJsonAsync<List<SettlementDto>>($"/groups/{groupId}/settlements");

        // Assert — 200 with empty array
        response.Should().NotBeNull();
        response!.Count.Should().Be(0, "should return an empty array when no settlements exist");
    }

    [Fact]
    public async Task ListSettlements_Ordering_NewestFirst()
    {
        // Arrange — two users, group, two settlements recorded with a gap
        var (clientA, userIdA) = await CreateAuthClientAsync("ordering_a@example.com", "OrderingA123!");

        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("ordering_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        var createGroupRequest = new CreateGroupRequest("Ordering Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var addMemberRequest = new AddMemberRequest("ordering_b@example.com");
        await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);

        // Record first settlement
        var settlement1 = new RecordSettlementRequest(userIdB, userIdA, 1000, "EUR", DateOnly.Parse("2024-06-01"));
        await clientA.PostAsJsonAsync($"/groups/{groupId}/settlements", settlement1);

        // Small delay to ensure different CreatedAt timestamps
        await Task.Delay(50);

        // Record second settlement
        var settlement2 = new RecordSettlementRequest(userIdA, userIdB, 2000, "EUR", DateOnly.Parse("2024-06-02"));
        await clientA.PostAsJsonAsync($"/groups/{groupId}/settlements", settlement2);

        // Act — GET /groups/{groupId}/settlements
        var response = await clientA.GetFromJsonAsync<List<SettlementDto>>($"/groups/{groupId}/settlements");

        // Assert — two settlements, newest (second) first
        response.Should().NotBeNull();
        response!.Count.Should().Be(2);
        response[0].AmountMinor.Should().Be(2000, "the second (newer) settlement should appear first");
        response[1].AmountMinor.Should().Be(1000, "the first (older) settlement should appear second");
    }

    [Fact]
    public async Task ListSettlements_SoftDeleted_ExcludedFromList()
    {
        // Arrange — two users, group, record a settlement, then soft-delete it
        var (clientA, userIdA) = await CreateAuthClientAsync("softdel_a@example.com", "SoftDelA123!");

        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("softdel_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        var createGroupRequest = new CreateGroupRequest("Soft Delete Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var addMemberRequest = new AddMemberRequest("softdel_b@example.com");
        await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);

        // Record a settlement
        var settlementRequest = new RecordSettlementRequest(userIdB, userIdA, 3000, "EUR", DateOnly.Parse("2024-06-15"));
        await clientA.PostAsJsonAsync($"/groups/{groupId}/settlements", settlementRequest);

        // Soft-delete the settlement directly in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SplitBook.Api.Infrastructure.Persistence.AppDbContext>();
        var settlement = await db.Settlements.IgnoreQueryFilters().FirstAsync(s => s.GroupId == groupId);
        settlement.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        // Act — GET /groups/{groupId}/settlements
        var response = await clientA.GetFromJsonAsync<List<SettlementDto>>($"/groups/{groupId}/settlements");

        // Assert — soft-deleted settlement is not in the list
        response.Should().NotBeNull();
        response!.Count.Should().Be(0, "soft-deleted settlements must be excluded from the list");
    }

    [Fact]
    public async Task ListSettlements_NonMember_Returns404()
    {
        // Arrange — user A creates a group, user B is not a member
        var (clientA, _) = await CreateAuthClientAsync("nonmember_list_a@example.com", "NonMemberListA123!");

        var createGroupRequest = new CreateGroupRequest("Non Member List Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var (clientB, _) = await CreateAuthClientAsync("nonmember_list_b@example.com", "NonMemberListB123!");

        // Act — user B (not a member) tries to list settlements
        var response = await clientB.GetAsync($"/groups/{groupId}/settlements");

        // Assert — 404 Not Found
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListSettlements_Unauthenticated_Returns401()
    {
        // Arrange — no auth header
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"/groups/{Guid.NewGuid()}/settlements");

        // Assert — 401 Unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
