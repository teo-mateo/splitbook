using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
using SplitBook.Api.Features.Auth.Login;
using SplitBook.Api.Features.Auth.Register;
using SplitBook.Api.Features.Balances.GetGroupBalances;
using SplitBook.Api.Features.Expenses.AddExpense;
using SplitBook.Api.Features.Groups.AddMember;
using SplitBook.Api.Features.Groups.CreateGroup;
using SplitBook.Api.Features.Groups.ListMyGroups;
using SplitBook.Api.Features.Settlements.RecordSettlement;
using SplitBook.Api.Tests.Infrastructure;
using Xunit;

namespace SplitBook.Api.Tests.Features.Settlements.RecordSettlement;

public class RecordSettlementEndpointTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public RecordSettlementEndpointTests(AppFactory factory)
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
    public async Task RecordSettlement_HappyPath_Returns201WithSettlementDto()
    {
        // Arrange — register user A and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("settlement_a@example.com", "SettlementA123!");

        // Register user B (separate call, no auth needed for register)
        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("settlement_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group (auto-added as member)
        var createGroupRequest = new CreateGroupRequest("Settlement Test Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("settlement_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act — POST /groups/{groupId}/settlements
        var settlementRequest = new RecordSettlementRequest(
            userIdB,
            userIdA,
            3000,
            "EUR",
            DateOnly.Parse("2024-06-15")
        );
        var response = await clientA.PostAsJsonAsync($"/groups/{groupId}/settlements", settlementRequest);
        var settlementDto = await response.ReadJsonAsync<SettlementDto>();

        // Assert — HTTP 201 Created
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert — response body is SettlementDto with correct fields
        settlementDto.Should().NotBeNull();
        settlementDto!.Id.Should().NotBe(Guid.Empty, "settlement must have a generated id");
        settlementDto.GroupId.Should().Be(groupId);
        settlementDto.FromUserId.Should().Be(userIdB, "fromUserId should be the payer (user B)");
        settlementDto.ToUserId.Should().Be(userIdA, "toUserId should be the payee (user A)");
        settlementDto.AmountMinor.Should().Be(3000);
        settlementDto.Currency.Should().Be("EUR");
        settlementDto.OccurredOn.Should().Be(DateOnly.Parse("2024-06-15"));

        // Assert — Location header points to /groups/{groupId}/settlements/{id}
        response.Headers.Location.Should().NotBeNull("201 response must include a Location header");
        var locationUri = response.Headers.Location!;
        var locationPath = locationUri.IsAbsoluteUri ? locationUri.AbsolutePath : locationUri.OriginalString;
        locationPath.Should().Be($"/groups/{groupId}/settlements/{settlementDto.Id}",
            "Location header must point to the newly created settlement resource");
    }

    [Fact]
    public async Task RecordSettlement_MovesBalances_BalancesReturnToZero()
    {
        // Arrange — register user A and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("balance_settlement_a@example.com", "BalanceSettlementA123!");

        // Register user B
        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("balance_settlement_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group (auto-added as member)
        var createGroupRequest = new CreateGroupRequest("Balance Settlement Test Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("balance_settlement_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Add an expense: 6000 minor units, paid by A, equal split between A and B
        var expenseRequest = new AddExpenseRequest(
            userIdA,
            6000,
            "EUR",
            "Dinner",
            DateOnly.Parse("2024-06-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new(userIdA, null, null, null),
                new(userIdB, null, null, null)
            }
        );
        var expenseResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        expenseResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Verify pre-settlement balances: A = +3000, B = -3000
        var balancesBeforeResponse = await clientA.GetFromJsonAsync<List<BalanceDto>>($"/groups/{groupId}/balances");
        balancesBeforeResponse.Should().NotBeNull();
        var balancesBefore = balancesBeforeResponse!;
        balancesBefore.Single(b => b.UserId == userIdA).NetAmountMinor.Should().Be(3000, "A paid 6000, owes 3000 share → net +3000");
        balancesBefore.Single(b => b.UserId == userIdB).NetAmountMinor.Should().Be(-3000, "B paid 0, owes 3000 share → net -3000");

        // Act — record settlement: B pays A 3000
        var settlementRequest = new RecordSettlementRequest(
            userIdB,
            userIdA,
            3000,
            "EUR",
            DateOnly.Parse("2024-06-15")
        );
        var settlementResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/settlements", settlementRequest);

        // Assert — settlement returns 201
        settlementResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert — balances are now zero for both users
        var balancesAfterResponse = await clientA.GetFromJsonAsync<List<BalanceDto>>($"/groups/{groupId}/balances");
        balancesAfterResponse.Should().NotBeNull();
        var balancesAfter = balancesAfterResponse!;
        balancesAfter.Single(b => b.UserId == userIdA).NetAmountMinor.Should().Be(0, "Settlement should bring A's balance to zero");
        balancesAfter.Single(b => b.UserId == userIdB).NetAmountMinor.Should().Be(0, "Settlement should bring B's balance to zero");
    }

    [Fact]
    public async Task RecordSettlement_BalanceInvariant_SumRemainsZero()
    {
        // Arrange — register user A and user B
        var (clientA, userIdA) = await CreateAuthClientAsync("invariant_a@example.com", "InvariantA123!");

        // Register user B
        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("invariant_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        // User A creates a group
        var createGroupRequest = new CreateGroupRequest("Invariant Test Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B as a member
        var addMemberRequest = new AddMemberRequest("invariant_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Add an expense: 6000, paid by A, equal split A+B → A=+3000, B=-3000
        var expenseRequest = new AddExpenseRequest(
            userIdA,
            6000,
            "EUR",
            "Dinner",
            DateOnly.Parse("2024-06-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new(userIdA, null, null, null),
                new(userIdB, null, null, null)
            }
        );
        (await clientA.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest)).StatusCode.Should().Be(HttpStatusCode.Created);

        // Record settlement: B pays A 1500 (partial settlement)
        var settlementRequest = new RecordSettlementRequest(
            userIdB,
            userIdA,
            1500,
            "EUR",
            DateOnly.Parse("2024-06-15")
        );
        var settlementResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/settlements", settlementRequest);
        settlementResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act — GET balances
        var balancesResponse = await clientA.GetFromJsonAsync<List<BalanceDto>>($"/groups/{groupId}/balances");
        balancesResponse.Should().NotBeNull();
        var balances = balancesResponse!;

        // Assert — invariant: sum of all netAmountMinor values equals exactly 0
        long sum = balances.Sum(b => b.NetAmountMinor);
        sum.Should().Be(0, "balances across the group must always sum to zero after a settlement (invariant)");

        // Assert — specific values: A=+1500 (3000-1500), B=-1500 (-3000+1500)
        balances.Single(b => b.UserId == userIdA).NetAmountMinor.Should().Be(1500, "A: +3000 from expense, -1500 from settlement = +1500");
        balances.Single(b => b.UserId == userIdB).NetAmountMinor.Should().Be(-1500, "B: -3000 from expense, +1500 from settlement = -1500");
    }

    [Fact]
    public async Task RecordSettlement_Unauthenticated_Returns401()
    {
        // Arrange — client with no Authorization header
        var client = _factory.CreateClient();

        var settlementRequest = new RecordSettlementRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            3000,
            "EUR",
            DateOnly.Parse("2024-06-15")
        );

        // Act
        var response = await client.PostAsJsonAsync($"/groups/{Guid.NewGuid()}/settlements", settlementRequest);

        // Assert — HTTP 401 Unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RecordSettlement_CallerNotMemberOfGroup_Returns404()
    {
        // Arrange — user A creates a group (auto-added as member)
        var (clientA, _) = await CreateAuthClientAsync("notmember_a@example.com", "NotMemberA123!");

        var createGroupRequest = new CreateGroupRequest("Private Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // User B is a separate user — NOT a member of the group
        var (clientB, userIdB) = await CreateAuthClientAsync("notmember_b@example.com", "NotMemberB123!");

        var settlementRequest = new RecordSettlementRequest(
            userIdB,
            userIdB,
            3000,
            "EUR",
            DateOnly.Parse("2024-06-15")
        );

        // Act — user B (not a member) tries to record a settlement for the group
        var response = await clientB.PostAsJsonAsync($"/groups/{groupId}/settlements", settlementRequest);

        // Assert — 404 Not Found (not 403) to avoid leaking group existence
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RecordSettlement_FromUserNotMember_Returns400()
    {
        // Arrange — register user A (group creator) and user B (not added to group)
        var (clientA, userIdA) = await CreateAuthClientAsync("from_nonmember_a@example.com", "FromNonMemberA123!");
        var (_, userIdB) = await RegisterAndLoginAsync("from_nonmember_b@example.com", "FromNonMemberB123!");

        // User A creates a group — only A is a member
        var createGroupRequest = new CreateGroupRequest("From Non-Member Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // User A tries to record a settlement where fromUserId is user B (not a member)
        var settlementRequest = new RecordSettlementRequest(
            userIdB,
            userIdA,
            3000,
            "EUR",
            DateOnly.Parse("2024-06-15")
        );

        // Act
        var response = await clientA.PostAsJsonAsync($"/groups/{groupId}/settlements", settlementRequest);

        // Assert — 400 Bad Request
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RecordSettlement_ToUserNotMember_Returns400()
    {
        // Arrange — register user A (group creator) and user B (not added to group)
        var (clientA, userIdA) = await CreateAuthClientAsync("to_nonmember_a@example.com", "ToNonMemberA123!");
        var (_, userIdB) = await RegisterAndLoginAsync("to_nonmember_b@example.com", "ToNonMemberB123!");

        // User A creates a group — only A is a member
        var createGroupRequest = new CreateGroupRequest("To Non-Member Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // User A tries to record a settlement where toUserId is user B (not a member)
        var settlementRequest = new RecordSettlementRequest(
            userIdA,
            userIdB,
            3000,
            "EUR",
            DateOnly.Parse("2024-06-15")
        );

        // Act
        var response = await clientA.PostAsJsonAsync($"/groups/{groupId}/settlements", settlementRequest);

        // Assert — 400 Bad Request
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RecordSettlement_FromEqualsTo_Returns400()
    {
        // Arrange
        var (client, userId) = await CreateAuthClientAsync("same_user@example.com", "SameUser123!");

        var createGroupRequest = new CreateGroupRequest("Same User Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // fromUserId == toUserId — cannot pay yourself
        var settlementRequest = new RecordSettlementRequest(
            userId,
            userId,
            3000,
            "EUR",
            DateOnly.Parse("2024-06-15")
        );

        // Act
        var response = await client.PostAsJsonAsync($"/groups/{groupId}/settlements", settlementRequest);

        // Assert — 400 Bad Request
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Assert — Problem+JSON shape
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problem.Should().NotBeNull();
        problem!.Type.Should().NotBeNull();
        problem.Title.Should().NotBeNull();
        problem.Status.Should().Be(400);
    }

    [Fact]
    public async Task RecordSettlement_ZeroAmount_Returns400()
    {
        // Arrange
        var (client, userId) = await CreateAuthClientAsync("zero_amount@example.com", "ZeroAmount123!");
        var (_, userIdB) = await RegisterAndLoginAsync("zero_amount_b@example.com", "ZeroAmountB123!");

        var createGroupRequest = new CreateGroupRequest("Zero Amount Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var addMemberRequest = new AddMemberRequest("zero_amount_b@example.com");
        await client.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);

        var settlementRequest = new RecordSettlementRequest(
            userId,
            userIdB,
            0,
            "EUR",
            DateOnly.Parse("2024-06-15")
        );

        // Act
        var response = await client.PostAsJsonAsync($"/groups/{groupId}/settlements", settlementRequest);

        // Assert — 400 Bad Request
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Assert — Problem+JSON shape
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(400);
    }

    [Fact]
    public async Task RecordSettlement_CurrencyMismatch_Returns400()
    {
        // Arrange
        var (client, userId) = await CreateAuthClientAsync("currency_mismatch@example.com", "CurrencyMismatch123!");
        var (_, userIdB) = await RegisterAndLoginAsync("currency_mismatch_b@example.com", "CurrencyMismatchB123!");

        var createGroupRequest = new CreateGroupRequest("Currency Mismatch Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var addMemberRequest = new AddMemberRequest("currency_mismatch_b@example.com");
        await client.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);

        // Group currency is EUR, but settlement uses USD
        var settlementRequest = new RecordSettlementRequest(
            userId,
            userIdB,
            3000,
            "USD",
            DateOnly.Parse("2024-06-15")
        );

        // Act
        var response = await client.PostAsJsonAsync($"/groups/{groupId}/settlements", settlementRequest);

        // Assert — 400 Bad Request
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Assert — Problem+JSON shape
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(400);
    }

    [Fact]
    public async Task RecordSettlement_IdempotencyKey_DuplicateRequest_ReturnsSameSettlementId()
    {
        // Arrange
        var (client, userId) = await CreateAuthClientAsync("idemp_settlement@example.com", "IdempSettlement123!");
        var (_, userIdB) = await RegisterAndLoginAsync("idemp_settlement_b@example.com", "IdempSettlementB123!");

        var createGroupRequest = new CreateGroupRequest("Idempotency Settlement Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var addMemberRequest = new AddMemberRequest("idemp_settlement_b@example.com");
        await client.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);

        var settlementRequest = new RecordSettlementRequest(
            userId,
            userIdB,
            3000,
            "EUR",
            DateOnly.Parse("2024-06-15")
        );

        var idempotencyKey = "settlement-key-456";
        var content = System.Text.Json.JsonSerializer.Serialize(settlementRequest);

        // Act — first POST with Idempotency-Key
        var request1 = new HttpRequestMessage(HttpMethod.Post, $"/groups/{groupId}/settlements")
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        };
        request1.Headers.Add("Idempotency-Key", idempotencyKey);
        var response1 = await client.SendAsync(request1);
        var result1 = await response1.ReadJsonAsync<SettlementDto>();

        // Act — second POST with the same Idempotency-Key
        var request2 = new HttpRequestMessage(HttpMethod.Post, $"/groups/{groupId}/settlements")
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        };
        request2.Headers.Add("Idempotency-Key", idempotencyKey);
        var response2 = await client.SendAsync(request2);
        var result2 = await response2.ReadJsonAsync<SettlementDto>();

        // Assert — both return 201
        response1.StatusCode.Should().Be(HttpStatusCode.Created);
        response2.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert — same settlement id
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1!.Id.Should().Be(result2!.Id, "idempotent requests must return the same settlement id");
    }

    [Fact]
    public async Task RecordSettlement_NonExistentGroup_Returns404()
    {
        // Arrange — authenticated user, but group ID does not exist
        var (client, _) = await CreateAuthClientAsync("nonexist_group_a@example.com", "NotExistGroupA123!");

        var settlementRequest = new RecordSettlementRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            3000,
            "EUR",
            DateOnly.Parse("2024-06-15")
        );

        // Act — POST to a group that was never created
        var response = await client.PostAsJsonAsync($"/groups/{Guid.NewGuid()}/settlements", settlementRequest);

        // Assert — 404 Not Found (not 403, to avoid leaking existence)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RecordSettlement_InvalidOccurredOn_Returns400()
    {
        // Arrange — register user A and user B, create group, add member
        var (clientA, userIdA) = await CreateAuthClientAsync("invalid_date_a@example.com", "InvalidDateA123!");

        var clientPlain = _factory.CreateClient();
        var registerB = new RegisterRequest("invalid_date_b@example.com", "User B", "PassB123!");
        var registerBResponse = await clientPlain.PostAsJsonAsync("/auth/register", registerB);
        registerBResponse.EnsureSuccessStatusCode();
        var registerBResult = await registerBResponse.ReadJsonAsync<RegisterResponse>();
        var userIdB = registerBResult!.Id;

        var createGroupRequest = new CreateGroupRequest("Invalid Date Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var addMemberRequest = new AddMemberRequest("invalid_date_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act — POST settlement with occurredOn omitted entirely (deserializes to DateOnly.MinValue)
        var missingOccurredOnJson = $@"{{
            ""fromUserId"": ""{userIdB}"",
            ""toUserId"": ""{userIdA}"",
            ""amountMinor"": 3000,
            ""currency"": ""EUR""
        }}";
        var content = new StringContent(missingOccurredOnJson, System.Text.Encoding.UTF8, "application/json");
        var response = await clientA.PostAsync($"/groups/{groupId}/settlements", content);

        // Assert — 400 Bad Request (OccurredOn is required; DateOnly.MinValue must be rejected)
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Assert — Problem+JSON shape
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(400);
    }

    [Fact]
    public async Task RecordSettlement_IdempotencyKey_OnlyOneRowCreated()
    {
        // Arrange
        var (client, userId) = await CreateAuthClientAsync("idemp_row_a@example.com", "IdempRowA123!");
        var (_, userIdB) = await RegisterAndLoginAsync("idemp_row_b@example.com", "IdempRowB123!");

        var createGroupRequest = new CreateGroupRequest("Idempotency Row Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var addMemberRequest = new AddMemberRequest("idemp_row_b@example.com");
        await client.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);

        var settlementRequest = new RecordSettlementRequest(
            userId,
            userIdB,
            3000,
            "EUR",
            DateOnly.Parse("2024-06-15")
        );

        var idempotencyKey = "row-count-key-17";
        var content = System.Text.Json.JsonSerializer.Serialize(settlementRequest);

        // Act — first POST with Idempotency-Key
        var request1 = new HttpRequestMessage(HttpMethod.Post, $"/groups/{groupId}/settlements")
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        };
        request1.Headers.Add("Idempotency-Key", idempotencyKey);
        var response1 = await client.SendAsync(request1);
        response1.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act — second POST with the same key (idempotent)
        var request2 = new HttpRequestMessage(HttpMethod.Post, $"/groups/{groupId}/settlements")
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        };
        request2.Headers.Add("Idempotency-Key", idempotencyKey);
        var response2 = await client.SendAsync(request2);
        response2.StatusCode.Should().Be(HttpStatusCode.Created);

        // Act — third POST with a different key (should create a new row)
        var request3 = new HttpRequestMessage(HttpMethod.Post, $"/groups/{groupId}/settlements")
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json")
        };
        request3.Headers.Add("Idempotency-Key", "different-key-18");
        var response3 = await client.SendAsync(request3);
        response3.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert — count rows in the database for this group: should be exactly 2 (one for first key, one for different key)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SplitBook.Api.Infrastructure.Persistence.AppDbContext>();
        var rowCount = await db.Settlements.IgnoreQueryFilters().CountAsync(s => s.GroupId == groupId);
        rowCount.Should().Be(2, "idempotent requests must create only one row; different key creates a second");
    }
}
