using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitBook.Api.Domain;
using SplitBook.Api.Features.Auth.Login;
using SplitBook.Api.Features.Auth.Register;
using SplitBook.Api.Features.Expenses.AddExpense;
using SplitBook.Api.Features.Groups.AddMember;
using SplitBook.Api.Features.Groups.CreateGroup;
using SplitBook.Api.Features.Groups.ListMyGroups;
using SplitBook.Api.Infrastructure.Persistence;
using SplitBook.Api.Tests.Infrastructure;
using Xunit;

namespace SplitBook.Api.Tests.Features.Expenses.AddExpense;

public class AddExpenseEndpointTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public AddExpenseEndpointTests(AppFactory factory)
    {
        _factory = factory;
    }

    private async Task<(string Token, Guid UserId)> RegisterAndLoginAsync(string email, string password)
    {
        var client = _factory.CreateClient();

        var registerRequest = new RegisterRequest(email, "TestUser", password);
        var registerResponse = await client.PostAsJsonAsync("/auth/register", registerRequest);
        registerResponse.EnsureSuccessStatusCode();
        var registerResult = await registerResponse.Content.ReadFromJsonAsync<RegisterResponse>();
        var userId = registerResult!.Id;

        var loginRequest = new LoginRequest(email, password);
        var loginResponse = await client.PostAsJsonAsync("/auth/login", loginRequest);
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>();

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
    public async Task AddExpense_EqualSplit_Returns201_WithExpenseDto()
    {
        // Arrange
        var (client, userId) = await CreateAuthClientAsync("expenseuser@example.com", "ExpensePass123!");
        var createGroupRequest = new CreateGroupRequest("Dinner Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.Content.ReadFromJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var expenseRequest = new AddExpenseRequest(
            userId,
            6000,
            "EUR",
            "Dinner",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );

        // Act
        var response = await client.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        var result = await response.Content.ReadFromJsonAsync<ExpenseDto>();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.Id.Should().NotBe(Guid.Empty);
        result.AmountMinor.Should().Be(6000);
        result.Currency.Should().Be("EUR");
        result.PayerUserId.Should().Be(userId);
        result.SplitMethod.Should().Be("Equal");
        result.Splits.Should().HaveCount(1);
        result.Splits[0].UserId.Should().Be(userId);
        result.Splits[0].AmountMinor.Should().Be(6000);
    }

    [Fact]
    public async Task AddExpense_EqualSplit_TwoParticipants_CreatesTwoSplitsOf3000Each()
    {
        // Arrange — register two users
        var (client, userIdA) = await CreateAuthClientAsync("splituser_a@example.com", "SplitPass123!");
        var (_, userIdB) = await RegisterAndLoginAsync("splituser_b@example.com", "SplitPass123!");

        // Create group and add user B
        var createGroupRequest = new CreateGroupRequest("Split Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.Content.ReadFromJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var addMemberRequest = new AddMemberRequest("splituser_b@example.com");
        var addMemberResponse = await client.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.EnsureSuccessStatusCode();

        var expenseRequest = new AddExpenseRequest(
            userIdA,
            6000,
            "EUR",
            "Dinner for two",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, null),
                new ExpenseSplitRequest(userIdB, null, null, null)
            }
        );

        // Act
        var response = await client.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        var result = await response.Content.ReadFromJsonAsync<ExpenseDto>();

        // Assert — HTTP response
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.Splits.Should().HaveCount(2);
        result.Splits.Should().AllSatisfy(s => s.AmountMinor.Should().Be(3000));

        // Assert — database has exactly 2 ExpenseSplit rows with AmountMinor = 3000
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expenseId = result.Id;
        var dbSplits = await db.ExpenseSplits
            .Where(es => es.ExpenseId == expenseId)
            .ToListAsync();
        dbSplits.Should().HaveCount(2);
        dbSplits.Should().AllSatisfy(es => es.AmountMinor.Should().Be(3000));
    }

    [Fact]
    public async Task AddExpense_EqualSplit_ThreeParticipants_AssignsRemainderToFirstParticipant()
    {
        // Arrange — register three users
        var (client, userIdA) = await CreateAuthClientAsync("round_a@example.com", "RoundPass123!");
        var (_, userIdB) = await RegisterAndLoginAsync("round_b@example.com", "RoundPass123!");
        var (_, userIdC) = await RegisterAndLoginAsync("round_c@example.com", "RoundPass123!");

        // Create group and add users B and C
        var createGroupRequest = new CreateGroupRequest("Rounding Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.Content.ReadFromJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var addMemberB = new AddMemberRequest("round_b@example.com");
        await client.PostAsJsonAsync($"/groups/{groupId}/members", addMemberB);

        var addMemberC = new AddMemberRequest("round_c@example.com");
        await client.PostAsJsonAsync($"/groups/{groupId}/members", addMemberC);

        // 100 minor units / 3 participants = 33 remainder 1 → first gets 34, rest get 33
        var expenseRequest = new AddExpenseRequest(
            userIdA,
            100,
            "EUR",
            "Rounding test",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, null),
                new ExpenseSplitRequest(userIdB, null, null, null),
                new ExpenseSplitRequest(userIdC, null, null, null)
            }
        );

        // Act
        var response = await client.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        var result = await response.Content.ReadFromJsonAsync<ExpenseDto>();

        // Assert — HTTP response
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        result.Should().NotBeNull();
        result!.Splits.Should().HaveCount(3);

        // Assert — remainder assigned to first participant in request order
        var splitA = result.Splits.Single(s => s.UserId == userIdA);
        var splitB = result.Splits.Single(s => s.UserId == userIdB);
        var splitC = result.Splits.Single(s => s.UserId == userIdC);

        splitA.AmountMinor.Should().Be(34, "first participant receives the remainder cent");
        splitB.AmountMinor.Should().Be(33);
        splitC.AmountMinor.Should().Be(33);

        // Assert — invariant: sum of split amounts equals expense total exactly
        result.Splits.Sum(s => s.AmountMinor).Should().Be(100, "split amounts must sum to the expense total");
    }

    [Fact]
    public async Task AddExpense_CallerNotMemberOfGroup_Returns404()
    {
        // Arrange — register user A, create group (A is the only member)
        var (clientA, userIdA) = await CreateAuthClientAsync("nonmember_a@example.com", "NonMemberPass123!");
        var createGroupRequest = new CreateGroupRequest("Private Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.Content.ReadFromJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Register and log in as user B (not a member of the group)
        var (clientB, userIdB) = await CreateAuthClientAsync("nonmember_b@example.com", "NonMemberPass123!");

        var expenseRequest = new AddExpenseRequest(
            userIdA,
            6000,
            "EUR",
            "Attempted expense",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, null)
            }
        );

        // Act — user B (not a member) tries to add an expense to the group
        var response = await clientB.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);

        // Assert — 404 Not Found (not 403) to avoid leaking group existence
        response.StatusCode.Should().Be(HttpStatusCode.NotFound, "non-members should receive 404, not 403");
    }

    [Fact]
    public async Task AddExpense_Unauthenticated_Returns401()
    {
        // Arrange — client with no Authorization header
        var client = _factory.CreateClient();

        var expenseRequest = new AddExpenseRequest(
            Guid.NewGuid(),
            6000,
            "EUR",
            "Unauthenticated expense",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(Guid.NewGuid(), null, null, null)
            }
        );

        // Act
        var response = await client.PostAsJsonAsync($"/groups/{Guid.NewGuid()}/expenses", expenseRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AddExpense_ZeroAmountMinor_Returns400_ProblemJson()
    {
        // Arrange
        var (client, userId) = await CreateAuthClientAsync("validation_zero@example.com", "ValidPass123!");
        var createGroupRequest = new CreateGroupRequest("Validation Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.Content.ReadFromJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var expenseRequest = new AddExpenseRequest(
            userId,
            0,
            "EUR",
            "Zero amount expense",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );

        // Act
        var response = await client.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);

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
    public async Task AddExpense_IdempotencyKey_DuplicateRequest_ReturnsSameExpenseId_AndSingleRow()
    {
        // Arrange
        var (client, userId) = await CreateAuthClientAsync("idempotency_user@example.com", "IdempPass123!");
        var createGroupRequest = new CreateGroupRequest("Idempotency Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.Content.ReadFromJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var expenseRequest = new AddExpenseRequest(
            userId,
            6000,
            "EUR",
            "Idempotent expense",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );

        var idempotencyKey = "test-key-123";

        // Act — first POST with Idempotency-Key
        var content1 = System.Text.Json.JsonSerializer.Serialize(expenseRequest);
        var request1 = new HttpRequestMessage(HttpMethod.Post, $"/groups/{groupId}/expenses")
        {
            Content = new StringContent(content1, System.Text.Encoding.UTF8, "application/json")
        };
        request1.Headers.Add("Idempotency-Key", idempotencyKey);
        var response1 = await client.SendAsync(request1);
        var result1 = await response1.Content.ReadFromJsonAsync<ExpenseDto>();

        // Act — second POST with the same Idempotency-Key and identical body
        var request2 = new HttpRequestMessage(HttpMethod.Post, $"/groups/{groupId}/expenses")
        {
            Content = new StringContent(content1, System.Text.Encoding.UTF8, "application/json")
        };
        request2.Headers.Add("Idempotency-Key", idempotencyKey);
        var response2 = await client.SendAsync(request2);
        var result2 = await response2.Content.ReadFromJsonAsync<ExpenseDto>();

        // Assert — both return 201
        response1.StatusCode.Should().Be(HttpStatusCode.Created);
        response2.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert — same expense id in both responses
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1!.Id.Should().Be(result2!.Id, "idempotent requests must return the same expense id");

        // Assert — database has exactly 1 Expense row (not 2)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expenseCount = await db.Expenses
            .Where(e => e.GroupId == groupId)
            .CountAsync();
        expenseCount.Should().Be(1, "only one Expense row should exist after idempotent duplicate");
    }

    [Fact]
    public async Task AddExpense_NoIdempotencyKey_EachRequestCreatesDistinctExpenseRow()
    {
        // Arrange
        var (client, userId) = await CreateAuthClientAsync("no_idempotency_user@example.com", "NoIdempPass123!");
        var createGroupRequest = new CreateGroupRequest("No Idempotency Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.Content.ReadFromJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var expenseRequest = new AddExpenseRequest(
            userId,
            6000,
            "EUR",
            "Non-idempotent expense",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );

        // Act — first POST without Idempotency-Key
        var response1 = await client.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        var result1 = await response1.Content.ReadFromJsonAsync<ExpenseDto>();

        // Act — second POST without Idempotency-Key (same body)
        var response2 = await client.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        var result2 = await response2.Content.ReadFromJsonAsync<ExpenseDto>();

        // Assert — both return 201 Created
        response1.StatusCode.Should().Be(HttpStatusCode.Created);
        response2.StatusCode.Should().Be(HttpStatusCode.Created);

        // Assert — different expense ids
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1!.Id.Should().NotBe(result2!.Id, "requests without Idempotency-Key must create distinct expenses");

        // Assert — database has exactly 2 Expense rows
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expenseCount = await db.Expenses
            .Where(e => e.GroupId == groupId)
            .CountAsync();
        expenseCount.Should().Be(2, "two requests without Idempotency-Key should create two Expense rows");
    }

    [Fact]
    public async Task AddExpense_PayerNotMemberOfGroup_Returns400()
    {
        // Arrange — register user A (group creator) and user B (not added to group)
        var (client, userIdA) = await CreateAuthClientAsync("payer_nonmember_a@example.com", "PayerPass123!");
        var (_, userIdB) = await RegisterAndLoginAsync("payer_nonmember_b@example.com", "PayerPass123!");

        // User A creates a group — only A is a member
        var createGroupRequest = new CreateGroupRequest("Payer Check Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.Content.ReadFromJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // User A tries to add an expense where payer is user B (not a member of the group)
        var expenseRequest = new AddExpenseRequest(
            userIdB,
            6000,
            "EUR",
            "Expense paid by non-member",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, null)
            }
        );

        // Act
        var response = await client.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);

        // Assert — 400 Bad Request because payer is not a group member
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, "payer must be a member of the group");

        // Assert — Problem+JSON shape
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(400);
    }
}
