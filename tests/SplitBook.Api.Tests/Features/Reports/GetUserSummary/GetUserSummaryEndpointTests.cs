using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using SplitBook.Api.Features.Auth.Login;
using SplitBook.Api.Features.Auth.Register;
using SplitBook.Api.Features.Balances.GetGroupBalances;
using SplitBook.Api.Features.Expenses.AddExpense;
using SplitBook.Api.Features.Groups.AddMember;
using SplitBook.Api.Features.Groups.CreateGroup;
using SplitBook.Api.Features.Groups.ListMyGroups;
using SplitBook.Api.Features.Settlements.RecordSettlement;
using SplitBook.Api.Features.Reports.GetUserSummary;
using SplitBook.Api.Tests.Infrastructure;
using Xunit;

namespace SplitBook.Api.Tests.Features.Reports.GetUserSummary;

public class GetUserSummaryEndpointTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public GetUserSummaryEndpointTests(AppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetUserSummary_Unauthenticated_Returns401()
    {
        // Arrange — client with no Authorization header
        var client = _factory.CreateClient();

        // Act — GET /users/me/summary without a JWT token
        var response = await client.GetAsync("/users/me/summary");

        // Assert — HTTP 401 Unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "an unauthenticated request should receive 401");
    }

    [Fact]
    public async Task GetUserSummary_EmptyMembership_Returns200WithEmptyGroups()
    {
        // Arrange — register and login a user who belongs to no groups
        var (client, _) = await CreateAuthClientAsync("summary_empty@example.com", "SummaryEmpty123!");

        // Act — GET /users/me/summary
        var response = await client.GetAsync("/users/me/summary");
        var summary = await response.Content.ReadFromJsonAsync<UserSummaryDto>();

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "an authenticated user should receive 200");

        // Assert — response body has an empty groups array
        summary.Should().NotBeNull();
        summary!.Groups.Should().BeEmpty(
            "a user who is not a member of any group should receive an empty groups array");
    }

    [Fact]
    public async Task GetUserSummary_ZeroActivity_ReturnsZeroBalances()
    {
        // Arrange — register user A and create a group (auto-added as member), no expenses or settlements
        var (clientA, userIdA) = await CreateAuthClientAsync("summary_zero@example.com", "ZeroActivity123!");

        var createGroupRequest = new CreateGroupRequest("Zero Activity Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Act — GET /users/me/summary as user A
        var response = await clientA.GetAsync("/users/me/summary");
        var summary = await response.Content.ReadFromJsonAsync<UserSummaryDto>();

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "an authenticated user should receive 200");

        // Assert — response body has exactly 1 group entry
        summary.Should().NotBeNull();
        summary!.Groups.Should().HaveCount(1,
            "user A belongs to exactly one group");

        // Assert — the group entry has zero net and zero gross
        var groupSummary = summary.Groups.Single(g => g.GroupId == groupId);
        groupSummary.NetAmountMinor.Should().Be(0,
            "a group with no expenses or settlements should have netAmountMinor of 0");
        groupSummary.GrossAmountMinor.Should().Be(0,
            "a group with no expenses or settlements should have grossAmountMinor of 0");
    }

    [Fact]
    public async Task GetUserSummary_NetMatchesBalances_ForSameGroupAndUser()
    {
        // Arrange — register user A and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("summary_netmatch_a@example.com", "NetMatchA123!");

        // Register user B
        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("summary_netmatch_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group (auto-added as member)
        var createGroupRequest = new CreateGroupRequest("Summary Net Match Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("summary_netmatch_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Add an expense: 6000 minor units, paid by A, equal split between A and B
        // A paid 6000, each owes 3000 → A net = +3000, B net = -3000
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

        // Act — GET /users/me/summary as user A
        var summaryResponse = await clientA.GetAsync("/users/me/summary");
        var summary = await summaryResponse.Content.ReadFromJsonAsync<UserSummaryDto>();

        // Act — GET /groups/{groupId}/balances as user A
        var balancesResponse = await clientA.GetAsync($"/groups/{groupId}/balances");
        var balances = await balancesResponse.Content.ReadFromJsonAsync<List<BalanceDto>>();

        // Assert — both endpoints return 200
        summaryResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        balancesResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — summary contains exactly 1 group entry
        summary.Should().NotBeNull();
        summary!.Groups.Should().HaveCount(1, "user A belongs to exactly one group");

        // Assert — find the group entry for this group in the summary
        var groupSummary = summary.Groups.Single(g => g.GroupId == groupId);

        // Assert — find the balance entry for user A in the balances endpoint
        balances.Should().NotBeNull();
        var balanceA = balances!.Single(b => b.UserId == userIdA);

        // Assert — netAmountMinor in the summary matches the balance endpoint for user A
        groupSummary.NetAmountMinor.Should().Be(balanceA.NetAmountMinor,
            "the summary netAmountMinor for a group must equal the balances endpoint netAmountMinor for the same user");

        // Assert — both should be +3000 (user A paid 6000, owes 3000)
        groupSummary.NetAmountMinor.Should().Be(3000, "user A paid 6000 and owes 3000, so net should be +3000");
    }

    [Fact]
    public async Task GetUserSummary_GrossEqualsPayerShare_EqualSplitExpense()
    {
        // Arrange — register user A (authenticated) and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("summary_gross_a@example.com", "GrossA123!");

        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("summary_gross_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group (auto-added as member)
        var createGroupRequest = new CreateGroupRequest("Gross Amount Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("summary_gross_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Add an expense: 6000 minor units (€60), paid by A, equal split between A and B
        // A paid 6000, each owes 3000 → A's share = 3000
        var expenseRequest = new AddExpenseRequest(
            userIdA,
            6000,
            "EUR",
            "Team dinner",
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

        // Act — GET /users/me/summary as user A
        var response = await clientA.GetAsync("/users/me/summary");
        var summary = await response.Content.ReadFromJsonAsync<UserSummaryDto>();

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — summary has exactly 1 group entry
        summary.Should().NotBeNull();
        summary!.Groups.Should().HaveCount(1);

        var groupSummary = summary.Groups.Single(g => g.GroupId == groupId);

        // Assert — grossAmountMinor equals the user's own share (3000), NOT the full expense (6000)
        groupSummary.GrossAmountMinor.Should().Be(3000,
            "grossAmountMinor should be the user's own share (3000 = €60 / 2 participants), not the full expense total (6000)");
    }

    [Fact]
    public async Task GetUserSummary_GrossEqualsNonPayerShare_EqualSplitExpense()
    {
        // Arrange — register user A (authenticated) and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("summary_gross_np_a@example.com", "GrossNPA123!");

        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("summary_gross_np_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group (auto-added as member)
        var createGroupRequest = new CreateGroupRequest("Non-Payer Gross Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("summary_gross_np_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Add an expense: 6000 minor units (€60), paid by A, equal split between A and B
        // A paid 6000, each owes 3000 → B's share = 3000 (B did not pay anything)
        var expenseRequest = new AddExpenseRequest(
            userIdA,
            6000,
            "EUR",
            "Team dinner",
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

        // Act — login user B (already registered above) and GET /users/me/summary
        var loginBRequest = new LoginRequest("summary_gross_np_b@example.com", "PassB123!");
        var loginBResponse = await clientPlain.PostAsJsonAsync("/auth/login", loginBRequest);
        var loginBResult = await loginBResponse.ReadJsonAsync<LoginResponse>();
        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginBResult!.AccessToken);
        var response = await clientB.GetAsync("/users/me/summary");
        var summary = await response.Content.ReadFromJsonAsync<UserSummaryDto>();

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — summary has exactly 1 group entry
        summary.Should().NotBeNull();
        summary!.Groups.Should().HaveCount(1);

        var groupSummary = summary.Groups.Single(g => g.GroupId == groupId);

        // Assert — grossAmountMinor equals the non-payer's share (3000), not 0
        groupSummary.GrossAmountMinor.Should().Be(3000,
            "grossAmountMinor for the non-payer should be their allocated share (3000 = €60 / 2), not 0");
    }

    [Fact]
    public async Task GetUserSummary_SettlementsDoNotAffectGross()
    {
        // Arrange — register user A (authenticated) and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("summary_settle_a@example.com", "SettleA123!");

        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("summary_settle_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group (auto-added as member)
        var createGroupRequest = new CreateGroupRequest("Settlement Gross Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("summary_settle_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Add an expense: 6000 minor units (€60), paid by A, equal split between A and B
        // A paid 6000, each owes 3000 → A's share = 3000, gross = 3000
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

        // Record a settlement: 3000 minor units, from B to A (B pays A to settle up)
        var settlementRequest = new RecordSettlementRequest(
            userIdB,
            userIdA,
            3000,
            "EUR",
            DateOnly.Parse("2024-06-16")
        );
        var settlementResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/settlements", settlementRequest);
        settlementResponse.EnsureSuccessStatusCode();

        // Act — GET /users/me/summary as user A
        var response = await clientA.GetAsync("/users/me/summary");
        var summary = await response.Content.ReadFromJsonAsync<UserSummaryDto>();

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — summary has exactly 1 group entry
        summary.Should().NotBeNull();
        summary!.Groups.Should().HaveCount(1);

        var groupSummary = summary.Groups.Single(g => g.GroupId == groupId);

        // Assert — grossAmountMinor remains 3000 (settlement did NOT change gross)
        groupSummary.GrossAmountMinor.Should().Be(3000,
            "grossAmountMinor should remain 3000 after a settlement — settlements affect net but never gross");
    }

    [Fact]
    public async Task GetUserSummary_SettlementsAffectNet_ZeroAfterSettlement()
    {
        // Arrange — register user A (authenticated) and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("summary_settle_net_a@example.com", "SettleNetA123!");

        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("summary_settle_net_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group (auto-added as member)
        var createGroupRequest = new CreateGroupRequest("Settlement Net Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("summary_settle_net_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Add an expense: 6000 minor units (€60), paid by A, equal split between A and B
        // A paid 6000, each owes 3000 → A net = +3000, B net = -3000
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

        // Verify initial state via balances: A = +3000, B = -3000
        var balancesBefore = await clientA.GetAsync($"/groups/{groupId}/balances");
        var balancesList = await balancesBefore.Content.ReadFromJsonAsync<List<BalanceDto>>();
        balancesList.Should().NotBeNull();
        var bl = balancesList!;
        bl.Single(b => b.UserId == userIdA).NetAmountMinor.Should().Be(3000);
        bl.Single(b => b.UserId == userIdB).NetAmountMinor.Should().Be(-3000);

        // Record a settlement: 3000 minor units, from B to A (clearing the debt)
        var settlementRequest = new RecordSettlementRequest(
            userIdB,
            userIdA,
            3000,
            "EUR",
            DateOnly.Parse("2024-06-16")
        );
        var settlementResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/settlements", settlementRequest);
        settlementResponse.EnsureSuccessStatusCode();

        // Act — GET /users/me/summary as user A
        var response = await clientA.GetAsync("/users/me/summary");
        var summary = await response.Content.ReadFromJsonAsync<UserSummaryDto>();

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — summary has exactly 1 group entry
        summary.Should().NotBeNull();
        summary!.Groups.Should().HaveCount(1);

        var groupSummary = summary.Groups.Single(g => g.GroupId == groupId);

        // Assert — netAmountMinor is 0 (settlement cleared the debt)
        groupSummary.NetAmountMinor.Should().Be(0,
            "after B settles €30 to A, A's netAmountMinor should be 0 — the debt is fully cleared");
    }

    [Fact]
    public async Task GetUserSummary_MultiGroupAggregation_ReturnsCorrectPerGroupNetAndGross()
    {
        // Arrange — register user A (authenticated caller) and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("summary_multi_a@example.com", "MultiA123!");

        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("summary_multi_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates group 1 (auto-added as member)
        var createGroup1Request = new CreateGroupRequest("Multi Group 1", "EUR");
        var createGroup1Response = await clientA.PostAsJsonAsync("/groups", createGroup1Request);
        var group1Dto = await createGroup1Response.ReadJsonAsync<GroupDto>();
        var groupId1 = group1Dto!.Id;

        // Add user B as member of group 1
        var addMember1Request = new AddMemberRequest("summary_multi_b@example.com");
        var addMember1Response = await clientA.PostAsJsonAsync($"/groups/{groupId1}/members", addMember1Request);
        addMember1Response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Expense in group 1: 4000 minor units, paid by A, equal split A+B
        // A paid 4000, each owes 2000 → A net = +2000, A gross = 2000
        var expense1Request = new AddExpenseRequest(
            userIdA,
            4000,
            "EUR",
            "Group 1 expense",
            DateOnly.Parse("2024-06-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, null),
                new ExpenseSplitRequest(userIdB, null, null, null)
            }
        );
        var addExpense1Response = await clientA.PostAsJsonAsync($"/groups/{groupId1}/expenses", expense1Request);
        addExpense1Response.EnsureSuccessStatusCode();

        // User A creates group 2 (auto-added as member)
        var createGroup2Request = new CreateGroupRequest("Multi Group 2", "EUR");
        var createGroup2Response = await clientA.PostAsJsonAsync("/groups", createGroup2Request);
        var group2Dto = await createGroup2Response.ReadJsonAsync<GroupDto>();
        var groupId2 = group2Dto!.Id;

        // Add user B as member of group 2
        var addMember2Request = new AddMemberRequest("summary_multi_b@example.com");
        var addMember2Response = await clientA.PostAsJsonAsync($"/groups/{groupId2}/members", addMember2Request);
        addMember2Response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Expense in group 2: 8000 minor units, paid by B, equal split A+B
        // B paid 8000, each owes 4000 → A net = -4000, A gross = 4000
        var expense2Request = new AddExpenseRequest(
            userIdB,
            8000,
            "EUR",
            "Group 2 expense",
            DateOnly.Parse("2024-06-16"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, null),
                new ExpenseSplitRequest(userIdB, null, null, null)
            }
        );
        var addExpense2Response = await clientA.PostAsJsonAsync($"/groups/{groupId2}/expenses", expense2Request);
        addExpense2Response.EnsureSuccessStatusCode();

        // Act — GET /users/me/summary as user A
        var response = await clientA.GetAsync("/users/me/summary");
        var summary = await response.Content.ReadFromJsonAsync<UserSummaryDto>();

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "an authenticated user should receive 200");

        // Assert — summary contains exactly 2 group entries
        summary.Should().NotBeNull();
        summary!.Groups.Should().HaveCount(2,
            "user A belongs to exactly two groups");

        // Assert — group 1 entry: netAmountMinor == 2000, grossAmountMinor == 2000
        var group1Summary = summary.Groups.Single(g => g.GroupId == groupId1);
        group1Summary.NetAmountMinor.Should().Be(2000,
            "user A paid 4000 and owes 2000 in group 1, so net should be +2000");
        group1Summary.GrossAmountMinor.Should().Be(2000,
            "user A's share in group 1 expense is 2000 (4000 / 2 participants)");

        // Assert — group 2 entry: netAmountMinor == -4000, grossAmountMinor == 4000
        var group2Summary = summary.Groups.Single(g => g.GroupId == groupId2);
        group2Summary.NetAmountMinor.Should().Be(-4000,
            "user A owes 4000 in group 2 (B paid 8000, A's share is 4000), so net should be -4000");
        group2Summary.GrossAmountMinor.Should().Be(4000,
            "user A's share in group 2 expense is 4000 (8000 / 2 participants)");
    }

    [Fact]
    public async Task GetUserSummary_SoftDeletedExpensesExcluded_RemovesNetAndGross()
    {
        // Arrange — register user A (authenticated) and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("summary_softdel_a@example.com", "SoftDelA123!");

        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("summary_softdel_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group (auto-added as member)
        var createGroupRequest = new CreateGroupRequest("Soft Delete Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("summary_softdel_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Add an expense: 6000 minor units, paid by A, equal split between A and B
        // A paid 6000, each owes 3000 → A net = +3000, A gross = 3000
        var expenseRequest = new AddExpenseRequest(
            userIdA,
            6000,
            "EUR",
            "Soft delete test expense",
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
        var expenseDto = await addExpenseResponse.Content.ReadFromJsonAsync<ExpenseDto>();
        var expenseId = expenseDto!.Id;

        // Verify initial state: A has netAmountMinor +3000, grossAmountMinor 3000
        var summaryBefore = await clientA.GetAsync("/users/me/summary");
        var summaryBeforeDto = await summaryBefore.Content.ReadFromJsonAsync<UserSummaryDto>();
        var groupBefore = summaryBeforeDto!.Groups.Single(g => g.GroupId == groupId);
        groupBefore.NetAmountMinor.Should().Be(3000, "before deletion, A paid 6000 and owes 3000, so net = +3000");
        groupBefore.GrossAmountMinor.Should().Be(3000, "before deletion, A's share is 3000 (6000 / 2)");

        // Act — soft-delete the expense
        var deleteRequest = clientA.DeleteAsync($"/groups/{groupId}/expenses/{expenseId}");
        var deleteResponse = await deleteRequest;
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "soft-deleting an expense should return 204");

        // Act — GET /users/me/summary as user A after deletion
        var summaryAfter = await clientA.GetAsync("/users/me/summary");
        var summaryAfterDto = await summaryAfter.Content.ReadFromJsonAsync<UserSummaryDto>();

        // Assert — HTTP 200
        summaryAfter.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — summary has exactly 1 group entry
        summaryAfterDto.Should().NotBeNull();
        summaryAfterDto!.Groups.Should().HaveCount(1);

        var groupAfter = summaryAfterDto.Groups.Single(g => g.GroupId == groupId);

        // Assert — the soft-deleted expense no longer contributes to net or gross
        groupAfter.NetAmountMinor.Should().Be(0,
            "after soft-deleting the only expense, netAmountMinor should be 0 — the deleted expense must be excluded");
        groupAfter.GrossAmountMinor.Should().Be(0,
            "after soft-deleting the only expense, grossAmountMinor should be 0 — the deleted expense must be excluded");
    }

    [Fact]
    public async Task ProductSpecE2E_FullScenario_Gross3000NetZeroForBothUsers()
    {
        // === Product-spec §8 end-to-end scenario ===
        // Two users register, one group, €60 equal-split expense paid by A,
        // €30 settlement from B to A → both users see grossAmountMinor=3000, netAmountMinor=0

        // Step 1: User A registers and logs in
        var (clientA, userIdA) = await CreateAuthClientAsync("e2e_user_a@example.com", "E2EUserA123!");

        // Step 2: User B registers (separate client)
        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("e2e_user_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // Step 3: User A creates a group ("Lisbon Trip", EUR)
        var createGroupRequest = new CreateGroupRequest("Lisbon Trip", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Step 4: User A adds user B by email as member
        var addMemberRequest = new AddMemberRequest("e2e_user_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "adding a member by email should return 204");

        // Step 5: User A adds an expense: 6000 minor units (€60), paid by A, equal split between A and B
        var expenseRequest = new AddExpenseRequest(
            userIdA,
            6000,
            "EUR",
            "Dinner in Lisbon",
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

        // Step 6: Verify balances — B owes A €30 (B net = -3000, A net = +3000)
        var balancesBefore = await clientA.GetAsync($"/groups/{groupId}/balances");
        var balancesBeforeList = await balancesBefore.Content.ReadFromJsonAsync<List<BalanceDto>>();
        balancesBeforeList.Should().NotBeNull();
        var balBefore = balancesBeforeList!;
        balBefore.Single(b => b.UserId == userIdA).NetAmountMinor.Should().Be(3000,
            "A paid 6000 and owes 3000, so net should be +3000");
        balBefore.Single(b => b.UserId == userIdB).NetAmountMinor.Should().Be(-3000,
            "B owes 3000, so net should be -3000");

        // Step 7: User B records a settlement: 3000 minor units (€30), from B to A
        // First, login user B to get their auth client
        var loginBRequest = new LoginRequest("e2e_user_b@example.com", "PassB123!");
        var loginBResponse = await clientPlain.PostAsJsonAsync("/auth/login", loginBRequest);
        var loginBResult = await loginBResponse.ReadJsonAsync<LoginResponse>();
        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginBResult!.AccessToken);

        var settlementRequest = new RecordSettlementRequest(
            userIdB,
            userIdA,
            3000,
            "EUR",
            DateOnly.Parse("2024-06-16")
        );
        var settlementResponse = await clientB.PostAsJsonAsync($"/groups/{groupId}/settlements", settlementRequest);
        settlementResponse.EnsureSuccessStatusCode();

        // Step 8: Verify balances — both now zero
        var balancesAfter = await clientA.GetAsync($"/groups/{groupId}/balances");
        var balancesAfterList = await balancesAfter.Content.ReadFromJsonAsync<List<BalanceDto>>();
        balancesAfterList.Should().NotBeNull();
        var balAfter = balancesAfterList!;
        balAfter.Single(b => b.UserId == userIdA).NetAmountMinor.Should().Be(0,
            "after B settles €30 to A, A's net should be 0");
        balAfter.Single(b => b.UserId == userIdB).NetAmountMinor.Should().Be(0,
            "after B settles €30 to A, B's net should be 0");

        // Step 9: Call GET /users/me/summary as user A
        var summaryAResponse = await clientA.GetAsync("/users/me/summary");
        var summaryA = await summaryAResponse.Content.ReadFromJsonAsync<UserSummaryDto>();

        // Step 10: Assert — 1 group entry, grossAmountMinor == 3000, netAmountMinor == 0
        summaryAResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        summaryA.Should().NotBeNull();
        summaryA!.Groups.Should().HaveCount(1, "user A belongs to exactly one group");

        var groupSummaryA = summaryA.Groups.Single(g => g.GroupId == groupId);
        groupSummaryA.GrossAmountMinor.Should().Be(3000,
            "user A's share of the €60 expense is €30 (3000 minor units) — gross should be 3000");
        groupSummaryA.NetAmountMinor.Should().Be(0,
            "after settlement, user A's net should be 0");

        // Step 11: Call GET /users/me/summary as user B
        var summaryBResponse = await clientB.GetAsync("/users/me/summary");
        var summaryB = await summaryBResponse.Content.ReadFromJsonAsync<UserSummaryDto>();

        // Step 12: Assert — 1 group entry, grossAmountMinor == 3000, netAmountMinor == 0
        summaryBResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        summaryB.Should().NotBeNull();
        summaryB!.Groups.Should().HaveCount(1, "user B belongs to exactly one group");

        var groupSummaryB = summaryB.Groups.Single(g => g.GroupId == groupId);
        groupSummaryB.GrossAmountMinor.Should().Be(3000,
            "user B's share of the €60 expense is €30 (3000 minor units) — gross should be 3000");
        groupSummaryB.NetAmountMinor.Should().Be(0,
            "after settlement, user B's net should be 0");
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
}
