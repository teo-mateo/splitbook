using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SplitBook.Api.Features.Auth.Login;
using SplitBook.Api.Features.Auth.Register;
using SplitBook.Api.Features.Balances.GetGroupBalances;
using SplitBook.Api.Features.Expenses.AddExpense;
using SplitBook.Api.Features.Groups.AddMember;
using SplitBook.Api.Features.Groups.CreateGroup;
using SplitBook.Api.Features.Groups.GetGroup;
using SplitBook.Api.Features.Groups.ListMyGroups;
using SplitBook.Api.Tests.Infrastructure;
using Xunit;

namespace SplitBook.Api.Tests.Features.Balances.GetGroupBalances;

public class GetGroupBalancesEndpointTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public GetGroupBalancesEndpointTests(AppFactory factory)
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
    public async Task GetGroupBalances_SingleEqualSplitExpense_ReturnsCorrectBalances()
    {
        // Arrange — register user A and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("balances_a@example.com", "BalancesA123!");

        // Register user B (separate client, no auth needed for register)
        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("balances_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group (auto-added as member)
        var createGroupRequest = new CreateGroupRequest("Balances Test Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("balances_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Add an expense: 6000 minor units, paid by A, equal split between A and B
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

        // Act — GET /groups/{groupId}/balances
        var response = await clientA.GetAsync($"/groups/{groupId}/balances");
        var balances = await response.Content.ReadFromJsonAsync<List<BalanceDto>>();

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — response contains exactly 2 entries
        balances.Should().NotBeNull();
        balances!.Should().HaveCount(2, "group has two members so there should be two balance entries");

        // Assert — user A has netAmountMinor = 3000 (positive — paid more than their share)
        var balanceA = balances!.Single(b => b.UserId == userIdA);
        balanceA.NetAmountMinor.Should().Be(3000, "user A paid 6000 and owes 3000, so net should be +3000");

        // Assert — user B has netAmountMinor = -3000 (negative — owes)
        var balanceB = balances!.Single(b => b.UserId == userIdB);
        balanceB.NetAmountMinor.Should().Be(-3000, "user B paid 0 and owes 3000, so net should be -3000");
    }

    [Fact]
    public async Task MultipleExpenses_CumulativeBalances()
    {
        // Arrange — register user A and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("balances_multi_a@example.com", "BalancesMultiA123!");

        // Register user B
        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("balances_multi_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group
        var createGroupRequest = new CreateGroupRequest("Multi Expense Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("balances_multi_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Expense 1: 10000 minor units, paid by A, equal split between A and B
        // A paid 10000, each owes 5000 → A net +5000, B net -5000
        var expense1Request = new AddExpenseRequest(
            userIdA,
            10000,
            "EUR",
            "Dinner",
            DateOnly.Parse("2024-06-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, null),
                new ExpenseSplitRequest(userIdB, null, null, null)
            }
        );
        var addExpense1Response = await clientA.PostAsJsonAsync($"/groups/{groupId}/expenses", expense1Request);
        addExpense1Response.EnsureSuccessStatusCode();

        // Expense 2: 4000 minor units, paid by B, equal split between A and B
        // B paid 4000, each owes 2000 → A net -2000, B net +2000
        var expense2Request = new AddExpenseRequest(
            userIdB,
            4000,
            "EUR",
            "Groceries",
            DateOnly.Parse("2024-06-16"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, null),
                new ExpenseSplitRequest(userIdB, null, null, null)
            }
        );
        var addExpense2Response = await clientA.PostAsJsonAsync($"/groups/{groupId}/expenses", expense2Request);
        addExpense2Response.EnsureSuccessStatusCode();

        // Act — GET /groups/{groupId}/balances
        var response = await clientA.GetAsync($"/groups/{groupId}/balances");
        var balances = await response.Content.ReadFromJsonAsync<List<BalanceDto>>();

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — response contains exactly 2 entries
        balances.Should().NotBeNull();
        balances!.Should().HaveCount(2, "group has two members so there should be two balance entries");

        // Assert — user A: +5000 (from expense 1) - 2000 (from expense 2) = +3000
        var balanceA = balances!.Single(b => b.UserId == userIdA);
        balanceA.NetAmountMinor.Should().Be(3000, "user A paid 10000 and owes 7000 total, so net should be +3000");

        // Assert — user B: -5000 (from expense 1) + 2000 (from expense 2) = -3000
        var balanceB = balances!.Single(b => b.UserId == userIdB);
        balanceB.NetAmountMinor.Should().Be(-3000, "user B paid 4000 and owes 7000 total, so net should be -3000");
    }

    [Fact]
    public async Task EmptyGroup_NoExpenses_AllZeroBalances()
    {
        // Arrange — register user A and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("balances_empty_a@example.com", "BalancesEmptyA123!");

        // Register user B
        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("balances_empty_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group (auto-added as member)
        var createGroupRequest = new CreateGroupRequest("Empty Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("balances_empty_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // NOTE: No expenses are added — this is an empty group

        // Act — GET /groups/{groupId}/balances
        var response = await clientA.GetAsync($"/groups/{groupId}/balances");
        var balances = await response.Content.ReadFromJsonAsync<List<BalanceDto>>();

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — response contains exactly 2 entries (one per active member)
        balances.Should().NotBeNull();
        balances!.Should().HaveCount(2, "group has two active members so there should be two balance entries");

        // Assert — both entries have netAmountMinor = 0 (no expenses exist)
        var balanceA = balances!.Single(b => b.UserId == userIdA);
        balanceA.NetAmountMinor.Should().Be(0, "user A has no expenses so net balance should be 0");

        var balanceB = balances!.Single(b => b.UserId == userIdB);
        balanceB.NetAmountMinor.Should().Be(0, "user B has no expenses so net balance should be 0");
    }

    [Fact]
    public async Task NonMemberCaller_Returns404WithProblemJson()
    {
        // Arrange — user A creates a group (auto-added as member)
        var (clientA, _) = await CreateAuthClientAsync("balances_auth_a@example.com", "BalancesAuthA123!");

        var createGroupRequest = new CreateGroupRequest("Auth Test Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // User B is a separate user — NOT a member of the group
        var (clientB, _) = await CreateAuthClientAsync("balances_stranger_b@example.com", "StrangerB123!");

        // Act — user B tries to get balances for a group they don't belong to
        var response = await clientB.GetAsync($"/groups/{groupId}/balances");
        var problemBody = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();

        // Assert — must be 404 (not 403) to avoid leaking group existence
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a non-member should receive 404, not 403, to avoid leaking group existence");

        // Assert — RFC 7807 Problem+JSON shape
        problemBody.Should().NotBeNull();
        problemBody!.Type.Should().NotBeNullOrEmpty("Problem+JSON must include a 'type' field");
        problemBody.Title.Should().NotBeNullOrEmpty("Problem+JSON must include a 'title' field");
        problemBody.Status.Should().Be(404, "Problem+JSON 'status' must match the HTTP status code");
    }

    [Fact]
    public async Task GetGroupBalances_Unauthenticated_Returns401()
    {
        // Arrange — client with no Authorization header
        var client = _factory.CreateClient();

        // Act — GET balances without a JWT token
        var response = await client.GetAsync($"/groups/{Guid.NewGuid()}/balances");

        // Assert — HTTP 401 Unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "an unauthenticated request should receive 401");
    }

    [Fact]
    public async Task GroupDoesNotExist_Returns404WithProblemJson()
    {
        // Arrange — register and login user A
        var (clientA, _) = await CreateAuthClientAsync("balances_notfound_a@example.com", "NotFoundA123!");

        // Use a GUID that does not correspond to any group
        var nonexistentGroupId = Guid.NewGuid();

        // Act — GET /groups/{nonexistent}/balances
        var response = await clientA.GetAsync($"/groups/{nonexistentGroupId}/balances");
        var problemBody = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();

        // Assert — HTTP 404
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "requesting balances for a non-existent group should return 404");

        // Assert — RFC 7807 Problem+JSON shape
        problemBody.Should().NotBeNull();
        problemBody!.Type.Should().NotBeNullOrEmpty("Problem+JSON must include a 'type' field");
        problemBody.Title.Should().NotBeNullOrEmpty("Problem+JSON must include a 'title' field");
        problemBody.Status.Should().Be(404, "Problem+JSON 'status' must match the HTTP status code");
    }

    [Fact]
    public async Task SoftDeletedExpense_ExcludedFromBalances()
    {
        // Arrange — register user A and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("balances_deleted_a@example.com", "DeletedA123!");

        // Register user B
        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("balances_deleted_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group (auto-added as member)
        var createGroupRequest = new CreateGroupRequest("Deleted Expense Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("balances_deleted_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Add an expense: 6000 minor units, paid by A, equal split between A and B
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
        var expenseDto = await addExpenseResponse.ReadJsonAsync<ExpenseDto>();
        var expenseId = expenseDto!.Id;

        // Verify initial balances: A: +3000, B: -3000
        var initialBalancesResponse = await clientA.GetAsync($"/groups/{groupId}/balances");
        var initialBalances = await initialBalancesResponse.Content.ReadFromJsonAsync<List<BalanceDto>>();
        initialBalances.Should().NotBeNull();
        initialBalances!.Single(b => b.UserId == userIdA).NetAmountMinor.Should().Be(3000);
        initialBalances!.Single(b => b.UserId == userIdB).NetAmountMinor.Should().Be(-3000);

        // Act — soft-delete the expense
        var deleteResponse = await clientA.DeleteAsync($"/groups/{groupId}/expenses/{expenseId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent, "soft delete should succeed with 204");

        // Act — GET /groups/{groupId}/balances after deletion
        var response = await clientA.GetAsync($"/groups/{groupId}/balances");
        var balances = await response.Content.ReadFromJsonAsync<List<BalanceDto>>();

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — response contains exactly 2 entries
        balances.Should().NotBeNull();
        balances!.Should().HaveCount(2, "group has two members so there should be two balance entries");

        // Assert — both users now have netAmountMinor = 0 (the deleted expense is excluded)
        var balanceA = balances!.Single(b => b.UserId == userIdA);
        balanceA.NetAmountMinor.Should().Be(0, "user A's balance should be 0 after their expense is soft-deleted");

        var balanceB = balances!.Single(b => b.UserId == userIdB);
        balanceB.NetAmountMinor.Should().Be(0, "user B's balance should be 0 after the expense affecting them is soft-deleted");
    }

    [Fact]
    public async Task ExactSplit_BalancesReflectExactShares()
    {
        // Arrange — register user A and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("balances_exact_a@example.com", "ExactA123!");

        // Register user B
        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("balances_exact_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group (auto-added as member)
        var createGroupRequest = new CreateGroupRequest("Exact Split Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("balances_exact_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Add an expense: 9000 minor units, paid by A, Exact split — A owes 3000, B owes 6000
        // Expected balances: A = +9000 (paid) - 3000 (owed share) = +6000; B = 0 - 6000 = -6000
        var expenseRequest = new AddExpenseRequest(
            userIdA,
            9000,
            "EUR",
            "Exact split dinner",
            DateOnly.Parse("2024-06-15"),
            "Exact",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, 3000, null, null),
                new ExpenseSplitRequest(userIdB, 6000, null, null)
            }
        );
        var addExpenseResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        addExpenseResponse.EnsureSuccessStatusCode();

        // Act — GET /groups/{groupId}/balances
        var response = await clientA.GetAsync($"/groups/{groupId}/balances");
        var balances = await response.Content.ReadFromJsonAsync<List<BalanceDto>>();

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — response contains exactly 2 entries
        balances.Should().NotBeNull();
        balances!.Should().HaveCount(2, "group has two members so there should be two balance entries");

        // Assert — user A: paid 9000, owes 3000 → net +6000
        var balanceA = balances!.Single(b => b.UserId == userIdA);
        balanceA.NetAmountMinor.Should().Be(6000, "user A paid 9000 and owes 3000, so net should be +6000");

        // Assert — user B: paid 0, owes 6000 → net -6000
        var balanceB = balances!.Single(b => b.UserId == userIdB);
        balanceB.NetAmountMinor.Should().Be(-6000, "user B paid 0 and owes 6000, so net should be -6000");
    }

    [Fact]
    public async Task PercentageSplit_BalancesReflectPercentageShares()
    {
        // Arrange — register user A and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("balances_pct_a@example.com", "PctA123!");

        // Register user B
        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("balances_pct_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group (auto-added as member)
        var createGroupRequest = new CreateGroupRequest("Percentage Split Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("balances_pct_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Add an expense: 10000 minor units, paid by A, Percentage split — A: 30%, B: 70%
        // Expected: A owes 3000, B owes 7000
        // Expected balances: A = +10000 (paid) - 3000 (owed share) = +7000; B = 0 - 7000 = -7000
        var expenseRequest = new AddExpenseRequest(
            userIdA,
            10000,
            "EUR",
            "Percentage split dinner",
            DateOnly.Parse("2024-06-15"),
            "Percentage",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, 30.0, null),
                new ExpenseSplitRequest(userIdB, null, 70.0, null)
            }
        );
        var addExpenseResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        addExpenseResponse.EnsureSuccessStatusCode();

        // Act — GET /groups/{groupId}/balances
        var response = await clientA.GetAsync($"/groups/{groupId}/balances");
        var balances = await response.Content.ReadFromJsonAsync<List<BalanceDto>>();

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — response contains exactly 2 entries
        balances.Should().NotBeNull();
        balances!.Should().HaveCount(2, "group has two members so there should be two balance entries");

        // Assert — user A: paid 10000, owes 3000 → net +7000
        var balanceA = balances!.Single(b => b.UserId == userIdA);
        balanceA.NetAmountMinor.Should().Be(7000, "user A paid 10000 and owes 3000 (30%), so net should be +7000");

        // Assert — user B: paid 0, owes 7000 → net -7000
        var balanceB = balances!.Single(b => b.UserId == userIdB);
        balanceB.NetAmountMinor.Should().Be(-7000, "user B paid 0 and owes 7000 (70%), so net should be -7000");
    }

    [Fact]
    public async Task SharesSplit_BalancesReflectShareProportions()
    {
        // Arrange — register user A and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("balances_shares_a@example.com", "SharesA123!");

        // Register user B
        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("balances_shares_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group (auto-added as member)
        var createGroupRequest = new CreateGroupRequest("Shares Split Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("balances_shares_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Add an expense: 12000 minor units, paid by A, Shares split — A: 1 share, B: 3 shares
        // Total = 4 shares, each share = 3000. A owes 3000, B owes 9000.
        // Expected balances: A = +12000 (paid) - 3000 (owed share) = +9000; B = 0 - 9000 = -9000
        var expenseRequest = new AddExpenseRequest(
            userIdA,
            12000,
            "EUR",
            "Shares split dinner",
            DateOnly.Parse("2024-06-15"),
            "Shares",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, 1),
                new ExpenseSplitRequest(userIdB, null, null, 3)
            }
        );
        var addExpenseResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        addExpenseResponse.EnsureSuccessStatusCode();

        // Act — GET /groups/{groupId}/balances
        var response = await clientA.GetAsync($"/groups/{groupId}/balances");
        var balances = await response.Content.ReadFromJsonAsync<List<BalanceDto>>();

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — response contains exactly 2 entries
        balances.Should().NotBeNull();
        balances!.Should().HaveCount(2, "group has two members so there should be two balance entries");

        // Assert — user A: paid 12000, owes 3000 (1/4 share) → net +9000
        var balanceA = balances!.Single(b => b.UserId == userIdA);
        balanceA.NetAmountMinor.Should().Be(9000, "user A paid 12000 and owes 3000 (1 of 4 shares), so net should be +9000");

        // Assert — user B: paid 0, owes 9000 (3/4 shares) → net -9000
        var balanceB = balances!.Single(b => b.UserId == userIdB);
        balanceB.NetAmountMinor.Should().Be(-9000, "user B paid 0 and owes 9000 (3 of 4 shares), so net should be -9000");
    }

    [Fact]
    public async Task MixedSplitMethods_BalancesSumToZero()
    {
        // Arrange — register user A and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("balances_mixed_a@example.com", "MixedA123!");

        // Register user B
        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("balances_mixed_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group
        var createGroupRequest = new CreateGroupRequest("Mixed Split Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("balances_mixed_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Expense 1 — Equal: 4000, paid by A, equal split A+B
        // Each owes 2000. A: +4000-2000=+2000, B: 0-2000=-2000
        var expenseEqual = new AddExpenseRequest(
            userIdA,
            4000,
            "EUR",
            "Equal split expense",
            DateOnly.Parse("2024-06-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, null),
                new ExpenseSplitRequest(userIdB, null, null, null)
            }
        );
        (await clientA.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseEqual)).EnsureSuccessStatusCode();

        // Expense 2 — Exact: 3000, paid by A, A=1000, B=2000
        // A: +3000-1000=+2000, B: 0-2000=-2000
        var expenseExact = new AddExpenseRequest(
            userIdA,
            3000,
            "EUR",
            "Exact split expense",
            DateOnly.Parse("2024-06-16"),
            "Exact",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, 1000, null, null),
                new ExpenseSplitRequest(userIdB, 2000, null, null)
            }
        );
        (await clientA.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseExact)).EnsureSuccessStatusCode();

        // Expense 3 — Percentage: 5000, paid by B, A=40%, B=60%
        // A owes 2000, B owes 3000. B: +5000-3000=+2000, A: 0-2000=-2000
        var expensePercentage = new AddExpenseRequest(
            userIdB,
            5000,
            "EUR",
            "Percentage split expense",
            DateOnly.Parse("2024-06-17"),
            "Percentage",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, 40.0, null),
                new ExpenseSplitRequest(userIdB, null, 60.0, null)
            }
        );
        (await clientA.PostAsJsonAsync($"/groups/{groupId}/expenses", expensePercentage)).EnsureSuccessStatusCode();

        // Expense 4 — Shares: 8000, paid by A, A=1 share, B=3 shares
        // Total 4 shares, each share = 2000. A owes 2000, B owes 6000.
        // A: +8000-2000=+6000, B: 0-6000=-6000
        var expenseShares = new AddExpenseRequest(
            userIdA,
            8000,
            "EUR",
            "Shares split expense",
            DateOnly.Parse("2024-06-18"),
            "Shares",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, 1),
                new ExpenseSplitRequest(userIdB, null, null, 3)
            }
        );
        (await clientA.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseShares)).EnsureSuccessStatusCode();

        // Act — GET /groups/{groupId}/balances
        var response = await clientA.GetAsync($"/groups/{groupId}/balances");

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var balances = await response.Content.ReadFromJsonAsync<List<BalanceDto>>();
        balances.Should().NotBeNull();

        // Assert — invariant: sum of all netAmountMinor values equals exactly 0
        // Cumulative: A = +2000 + 2000 - 2000 + 6000 = +8000; B = -2000 - 2000 + 2000 - 6000 = -8000
        long sum = balances!.Sum(b => b.NetAmountMinor);
        sum.Should().Be(0, "balances across the group must always sum to zero regardless of split methods used (invariant)");
    }

    [Fact]
    public async Task BalancesSumToZero_SingleExpense()
    {
        // Arrange — register user A and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("balances_sum_a@example.com", "BalancesSumA123!");

        // Register user B
        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("balances_sum_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group
        var createGroupRequest = new CreateGroupRequest("Balances Sum Test Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("balances_sum_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Add an expense: 6000 minor units, paid by A, equal split between A and B
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

        // Act — GET /groups/{groupId}/balances
        var response = await clientA.GetAsync($"/groups/{groupId}/balances");

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var balances = await response.Content.ReadFromJsonAsync<List<BalanceDto>>();
        balances.Should().NotBeNull();

        // Assert — invariant: sum of all netAmountMinor values equals exactly 0
        long sum = balances!.Sum(b => b.NetAmountMinor);
        sum.Should().Be(0, "balances across the group must always sum to zero (invariant)");
    }
}
