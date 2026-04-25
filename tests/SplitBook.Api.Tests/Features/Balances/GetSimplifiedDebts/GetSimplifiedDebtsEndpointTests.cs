using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SplitBook.Api.Features.Auth.Login;
using SplitBook.Api.Features.Auth.Register;
using SplitBook.Api.Features.Balances.GetSimplifiedDebts;
using SplitBook.Api.Features.Expenses.AddExpense;
using SplitBook.Api.Features.Groups.AddMember;
using SplitBook.Api.Features.Groups.CreateGroup;
using SplitBook.Api.Features.Groups.ListMyGroups;
using SplitBook.Api.Features.Settlements.RecordSettlement;
using SplitBook.Api.Tests.Infrastructure;
using Xunit;

namespace SplitBook.Api.Tests.Features.Balances.GetSimplifiedDebts;

public class GetSimplifiedDebtsEndpointTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public GetSimplifiedDebtsEndpointTests(AppFactory factory)
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
    public async Task GetSimplifiedDebts_TwoUsers_OneOwesTheOther_ReturnsSingleTransfer()
    {
        // Arrange — register user A and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("debts_a@example.com", "DebtsA123!");

        // Register user B (separate client, no auth needed for register)
        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("debts_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group (auto-added as member)
        var createGroupRequest = new CreateGroupRequest("Debts Test Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("debts_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Add an expense: 6000 minor units, paid by A, equal split between A and B
        // A paid 6000, each owes 3000 → A balance = +3000, B balance = -3000
        var expenseRequest = new AddExpenseRequest(
            userIdA,
            6000,
            "EUR",
            "Team lunch",
            DateOnly.Parse("2024-06-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, null),
                new ExpenseSplitRequest(userIdB, null, null, null)
            }
        );
        var addExpenseResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        addExpenseResponse.EnsureSuccessStatusCode();

        // Act — GET /groups/{groupId}/simplified-debts
        var response = await clientA.GetAsync($"/groups/{groupId}/simplified-debts");

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var debts = await response.Content.ReadFromJsonAsync<List<SimplifiedDebtDto>>();

        // Assert — exactly 1 transfer
        debts.Should().NotBeNull();
        debts!.Should().HaveCount(1, "two users with opposite balances should produce exactly one transfer");

        // Assert — the transfer is B → A for 3000 minor units
        var transfer = debts!.Single();
        transfer.FromUserId.Should().Be(userIdB, "user B owes money, so they should be the payer");
        transfer.ToUserId.Should().Be(userIdA, "user A is owed money, so they should be the payee");
        transfer.AmountMinor.Should().Be(3000, "B owes A exactly 3000 minor units (half of 6000)");
    }

    [Fact]
    public async Task GetSimplifiedDebts_ThreeUsers_ReducesTransferCount_ToAtMostNMinus1()
    {
        // Arrange — register users A, B, C
        var (clientA, userIdA) = await CreateAuthClientAsync("simplify_a@example.com", "SimplifyA123!");

        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("simplify_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        var registerC = new RegisterRequest("simplify_c@example.com", "User C", "PassC123!");
        var registerCResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerC);
        registerCResponse.EnsureSuccessStatusCode();
        var registerCResult = await registerCResponse.ReadJsonAsync<RegisterResponse>();
        var userIdC = registerCResult!.Id;

        // User A creates a group (auto-added as member)
        var createGroupRequest = new CreateGroupRequest("Simplify Test Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add users B and C as members
        var addMemberBRequest = new AddMemberRequest("simplify_b@example.com");
        var addMemberBResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberBRequest);
        addMemberBResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var addMemberCRequest = new AddMemberRequest("simplify_c@example.com");
        var addMemberCResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberCRequest);
        addMemberCResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Add an expense: 5000 minor units, paid by A, Exact split — B owes 3000, C owes 2000
        // Balances: A = +5000, B = −3000, C = −2000
        var expenseRequest = new AddExpenseRequest(
            userIdA,
            5000,
            "EUR",
            "Dinner",
            DateOnly.Parse("2024-06-15"),
            "Exact",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdB, 3000, null, null),
                new ExpenseSplitRequest(userIdC, 2000, null, null)
            }
        );
        var addExpenseResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        addExpenseResponse.EnsureSuccessStatusCode();

        // Act — GET /groups/{groupId}/simplified-debts
        var response = await clientA.GetAsync($"/groups/{groupId}/simplified-debts");

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var debts = await response.Content.ReadFromJsonAsync<List<SimplifiedDebtDto>>();
        debts.Should().NotBeNull();

        // Assert — at most N−1 = 2 transfers (not 3)
        debts!.Count.Should().BeLessThanOrEqualTo(2, "simplified debts for 3 non-zero members must produce at most 2 transfers (≤ N−1)");

        // Assert — executing the transfers would zero all balances
        // Known balances: A = +5000, B = −3000, C = −2000
        var balanceMap = new Dictionary<Guid, long>
        {
            { userIdA, 5000 },
            { userIdB, -3000 },
            { userIdC, -2000 }
        };

        // Simulate executing transfers: debtor pays → their balance increases (less negative),
        // creditor receives → their balance decreases (less positive)
        foreach (var debt in debts!)
        {
            balanceMap[debt.FromUserId] += debt.AmountMinor;
            balanceMap[debt.ToUserId] -= debt.AmountMinor;
        }

        foreach (var kvp in balanceMap)
        {
            kvp.Value.Should().Be(0, $"executing simplified debts should zero balance for user {kvp.Key}");
        }
    }

    [Fact]
    public async Task GetSimplifiedDebts_AllZeroBalances_ReturnsEmptyList()
    {
        // Arrange — register user A and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("emptydebts_a@example.com", "EmptyDebtsA123!");

        // Register user B (separate client, no auth needed for register)
        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("emptydebts_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();

        // User A creates a group (auto-added as member)
        var createGroupRequest = new CreateGroupRequest("Empty Debts Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("emptydebts_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // No expenses are added — all balances are 0

        // Act — GET /groups/{groupId}/simplified-debts
        var response = await clientA.GetAsync($"/groups/{groupId}/simplified-debts");

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var debts = await response.Content.ReadFromJsonAsync<List<SimplifiedDebtDto>>();

        // Assert — empty list (no debts to simplify when all balances are zero)
        debts.Should().NotBeNull();
        debts!.Should().BeEmpty("with no expenses, all balances are zero so there should be no transfers");
    }

    [Fact]
    public async Task GetSimplifiedDebts_NonMemberCaller_Returns404WithProblemJson()
    {
        // Arrange — register user A (group creator) and user B (not a member)
        var (clientA, _) = await CreateAuthClientAsync("auth_a@example.com", "AuthA123!");
        var (clientB, _) = await CreateAuthClientAsync("auth_b@example.com", "AuthB123!");

        // User A creates a group (auto-added as member); user B is NOT added
        var createGroupRequest = new CreateGroupRequest("Auth Test Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Act — user B (non-member) calls GET /groups/{groupId}/simplified-debts
        var response = await clientB.GetAsync($"/groups/{groupId}/simplified-debts");

        // Assert — HTTP 404 (not 403, to avoid leaking group existence)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Assert — RFC 7807 Problem+JSON shape
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problem.Should().NotBeNull();
        problem!.Type.Should().NotBeNullOrEmpty("Problem+JSON must include a non-empty Type field");
        problem.Title.Should().NotBeNullOrEmpty("Problem+JSON must include a non-empty Title field");
        problem.Status.Should().Be(404, "Problem+JSON Status must match the HTTP status code");
    }

    [Fact]
    public async Task GetSimplifiedDebts_Unauthenticated_Returns401()
    {
        // Arrange — client with no Authorization header
        var client = _factory.CreateClient();

        // Act — GET simplified-debts without a JWT token
        var response = await client.GetAsync($"/groups/{Guid.NewGuid()}/simplified-debts");

        // Assert — HTTP 401 Unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "an unauthenticated request should receive 401");
    }

    [Fact]
    public async Task GetSimplifiedDebts_NonExistentGroup_Returns404WithProblemJson()
    {
        // Arrange — register and login user A (authenticated, but no groups created)
        var (clientA, _) = await CreateAuthClientAsync("nonexist_a@example.com", "NonExistA123!");

        // Use a GUID that does not correspond to any group
        var nonexistentGroupId = Guid.NewGuid();

        // Act — GET /groups/{nonexistentGroupId}/simplified-debts
        var response = await clientA.GetAsync($"/groups/{nonexistentGroupId}/simplified-debts");

        // Assert — HTTP 404
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Assert — RFC 7807 Problem+JSON shape
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problem.Should().NotBeNull();
        problem!.Type.Should().NotBeNullOrEmpty("Problem+JSON must include a non-empty Type field");
        problem.Title.Should().NotBeNullOrEmpty("Problem+JSON must include a non-empty Title field");
        problem.Status.Should().Be(404, "Problem+JSON Status must match the HTTP status code");
    }

    [Fact]
    public async Task GetSimplifiedDebts_SettlementZeroesBalances_ReturnsEmptyList()
    {
        // Arrange — register user A and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("settle_a@example.com", "SettleA123!");

        // Register user B (separate client, no auth needed for register)
        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("settle_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group (auto-added as member)
        var createGroupRequest = new CreateGroupRequest("Settlement Debts Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("settle_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Add an expense: 6000 minor units, paid by A, equal split between A and B
        // A paid 6000, each owes 3000 → A balance = +3000, B balance = -3000
        var expenseRequest = new AddExpenseRequest(
            userIdA,
            6000,
            "EUR",
            "Team lunch",
            DateOnly.Parse("2024-06-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, null),
                new ExpenseSplitRequest(userIdB, null, null, null)
            }
        );
        var addExpenseResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        addExpenseResponse.EnsureSuccessStatusCode();

        // Record a settlement: B pays A 3000 → zeroes both balances
        var settlementRequest = new RecordSettlementRequest(
            userIdB,
            userIdA,
            3000,
            "EUR",
            DateOnly.Parse("2024-06-16")
        );
        var settlementResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/settlements", settlementRequest);
        settlementResponse.EnsureSuccessStatusCode();

        // Act — GET /groups/{groupId}/simplified-debts
        var response = await clientA.GetAsync($"/groups/{groupId}/simplified-debts");

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var debts = await response.Content.ReadFromJsonAsync<List<SimplifiedDebtDto>>();

        // Assert — empty list (settlement zeroed all balances, so no transfers needed)
        debts.Should().NotBeNull();
        debts!.Should().BeEmpty("after a settlement that zeroes all balances, simplified debts should return an empty list");
    }
}
