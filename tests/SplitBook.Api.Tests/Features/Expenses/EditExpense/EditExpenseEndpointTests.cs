using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitBook.Api.Domain;
using SplitBook.Api.Features.Auth.Login;
using SplitBook.Api.Features.Auth.Register;
using SplitBook.Api.Features.Expenses.AddExpense;
using SplitBook.Api.Features.Groups.CreateGroup;
using SplitBook.Api.Features.Groups.ListMyGroups;
using SplitBook.Api.Infrastructure.Persistence;
using SplitBook.Api.Tests.Infrastructure;
using Xunit;

namespace SplitBook.Api.Tests.Features.Expenses.EditExpense;

public class EditExpenseEndpointTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public EditExpenseEndpointTests(AppFactory factory)
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
    public async Task EditExpense_HappyPath_Returns200_WithUpdatedDescription_AndIncrementedVersion()
    {
        // Arrange — register user, create group, add an expense via POST
        var (client, userId) = await CreateAuthClientAsync("edit_expense_user@example.com", "EditPass123!");
        var createGroupRequest = new CreateGroupRequest("Edit Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var originalDescription = "Original description";
        var expenseRequest = new AddExpenseRequest(
            userId,
            6000,
            "EUR",
            originalDescription,
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );

        var postResponse = await client.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        var createdExpense = await postResponse.ReadJsonAsync<ExpenseDto>();
        createdExpense.Should().NotBeNull();
        var expenseId = createdExpense!.Id;
        var originalVersion = createdExpense.Version;

        // Act — PUT with updated description and If-Match header set to current version
        var updatedDescription = "Updated description";
        var editRequest = new AddExpenseRequest(
            userId,
            6000,
            "EUR",
            updatedDescription,
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );

        var putContent = System.Text.Json.JsonSerializer.Serialize(editRequest);
        var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/groups/{groupId}/expenses/{expenseId}")
        {
            Content = new StringContent(putContent, System.Text.Encoding.UTF8, "application/json")
        };
        putRequest.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue($"\"{originalVersion}\""));
        var putResponse = await client.SendAsync(putRequest);
        var editedExpense = await putResponse.ReadJsonAsync<ExpenseDto>();

        // Assert — 200 OK
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        editedExpense.Should().NotBeNull();

        // Assert — description updated
        editedExpense!.Description.Should().Be(updatedDescription);

        // Assert — Version incremented by exactly 1
        editedExpense.Version.Should().Be(originalVersion + 1, "Version should be incremented by exactly 1 after edit");

        // Assert — ExpenseSplit rows replaced (still 1 row for single participant)
        editedExpense.Splits.Should().HaveCount(1);
        editedExpense.Splits[0].UserId.Should().Be(userId);
        editedExpense.Splits[0].AmountMinor.Should().Be(6000);

        // Assert — database reflects the updated description and incremented version
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbExpense = await db.Expenses
            .SingleAsync(e => e.Id == expenseId);
        dbExpense.Description.Should().Be(updatedDescription);
        dbExpense.Version.Should().Be(originalVersion + 1);

        // Assert — database has exactly 1 ExpenseSplit row (old replaced, not duplicated)
        var dbSplits = await db.ExpenseSplits
            .Where(es => es.ExpenseId == expenseId)
            .ToListAsync();
        dbSplits.Should().HaveCount(1, "old ExpenseSplit rows should be replaced, not duplicated");
    }

    [Fact]
    public async Task EditExpense_StaleIfMatch_Returns412_ProblemJson()
    {
        // Arrange — register user, create group, add an expense via POST
        var (client, userId) = await CreateAuthClientAsync("edit_expense_stale_user@example.com", "StalePass123!");
        var createGroupRequest = new CreateGroupRequest("Stale Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var expenseRequest = new AddExpenseRequest(
            userId,
            6000,
            "EUR",
            "Original description",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );

        var postResponse = await client.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        var createdExpense = await postResponse.ReadJsonAsync<ExpenseDto>();
        createdExpense.Should().NotBeNull();
        var expenseId = createdExpense!.Id;

        // Act — PUT with a stale If-Match header (version "999" never existed)
        var editRequest = new AddExpenseRequest(
            userId,
            6000,
            "EUR",
            "Updated description",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );

        var putContent = System.Text.Json.JsonSerializer.Serialize(editRequest);
        var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/groups/{groupId}/expenses/{expenseId}")
        {
            Content = new StringContent(putContent, System.Text.Encoding.UTF8, "application/json")
        };
        putRequest.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue("\"999\""));
        var putResponse = await client.SendAsync(putRequest);

        // Assert — 412 Precondition Failed
        putResponse.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        // Assert — Problem+JSON body (type, title, status fields present)
        var problemBody = await putResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problemBody.Should().NotBeNull();
        problemBody!.Type.Should().NotBeNullOrEmpty();
        problemBody.Title.Should().NotBeNullOrEmpty();
        problemBody.Status.Should().Be(412);
    }

    [Fact]
    public async Task EditExpense_MissingIfMatch_Returns412_ProblemJson()
    {
        // Arrange — register user, create group, add an expense via POST
        var (client, userId) = await CreateAuthClientAsync("edit_expense_no_ifmatch_user@example.com", "NoIfMatch123!");
        var createGroupRequest = new CreateGroupRequest("No If-Match Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var expenseRequest = new AddExpenseRequest(
            userId,
            6000,
            "EUR",
            "Original description",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );

        var postResponse = await client.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        var createdExpense = await postResponse.ReadJsonAsync<ExpenseDto>();
        createdExpense.Should().NotBeNull();
        var expenseId = createdExpense!.Id;

        // Act — PUT with NO If-Match header at all
        var editRequest = new AddExpenseRequest(
            userId,
            6000,
            "EUR",
            "Updated description",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );

        var putContent = System.Text.Json.JsonSerializer.Serialize(editRequest);
        var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/groups/{groupId}/expenses/{expenseId}")
        {
            Content = new StringContent(putContent, System.Text.Encoding.UTF8, "application/json")
        };
        // Intentionally: no If-Match header set
        var putResponse = await client.SendAsync(putRequest);

        // Assert — 412 Precondition Failed
        putResponse.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);

        // Assert — Problem+JSON body (type, title, status fields present)
        var problemBody = await putResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problemBody.Should().NotBeNull();
        problemBody!.Type.Should().NotBeNullOrEmpty();
        problemBody.Title.Should().NotBeNullOrEmpty();
        problemBody.Status.Should().Be(412);
    }

    [Fact]
    public async Task EditExpense_NonExistentExpense_Returns404_ProblemJson()
    {
        // Arrange — register user, create group (no expense needed)
        var (client, userId) = await CreateAuthClientAsync("edit_expense_404_user@example.com", "FourOhFour123!");
        var createGroupRequest = new CreateGroupRequest("404 Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var fakeExpenseId = Guid.NewGuid();

        // Act — PUT against a non-existent expense ID
        var editRequest = new AddExpenseRequest(
            userId,
            6000,
            "EUR",
            "Updated description",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );

        var putContent = System.Text.Json.JsonSerializer.Serialize(editRequest);
        var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/groups/{groupId}/expenses/{fakeExpenseId}")
        {
            Content = new StringContent(putContent, System.Text.Encoding.UTF8, "application/json")
        };
        putRequest.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue("\"0\""));
        var putResponse = await client.SendAsync(putRequest);

        // Assert — 404 Not Found
        putResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Assert — Problem+JSON body (type, title, status fields present)
        var problemBody = await putResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problemBody.Should().NotBeNull();
        problemBody!.Type.Should().NotBeNullOrEmpty();
        problemBody.Title.Should().NotBeNullOrEmpty();
        problemBody.Status.Should().Be(404);
    }

    [Fact]
    public async Task EditExpense_CallerNotGroupMember_Returns404_ProblemJson()
    {
        // Arrange — register user A, create group (A is the only member), add an expense
        var (clientA, userIdA) = await CreateAuthClientAsync("edit_expense_owner@example.com", "OwnerPass123!");
        var createGroupRequest = new CreateGroupRequest("Owner Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var expenseRequest = new AddExpenseRequest(
            userIdA,
            6000,
            "EUR",
            "Original description",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, null)
            }
        );

        var postResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        var createdExpense = await postResponse.ReadJsonAsync<ExpenseDto>();
        createdExpense.Should().NotBeNull();
        var expenseId = createdExpense!.Id;
        var originalVersion = createdExpense.Version;

        // Register user B — NOT added to the group
        var (clientB, _) = await CreateAuthClientAsync("edit_expense_intruder@example.com", "IntruderPass123!");

        // Act — user B (not a group member) tries to PUT the expense
        var editRequest = new AddExpenseRequest(
            userIdA,
            6000,
            "EUR",
            "Updated description",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, null)
            }
        );

        var putContent = System.Text.Json.JsonSerializer.Serialize(editRequest);
        var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/groups/{groupId}/expenses/{expenseId}")
        {
            Content = new StringContent(putContent, System.Text.Encoding.UTF8, "application/json")
        };
        putRequest.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue($"\"{originalVersion}\""));
        var putResponse = await clientB.SendAsync(putRequest);

        // Assert — 404 Not Found (NOT 403 Forbidden — spec §5: avoid leaking existence)
        putResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Assert — Problem+JSON body (type, title, status fields present)
        var problemBody = await putResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problemBody.Should().NotBeNull();
        problemBody!.Type.Should().NotBeNullOrEmpty();
        problemBody.Title.Should().NotBeNullOrEmpty();
        problemBody.Status.Should().Be(404);
    }

    [Fact]
    public async Task EditExpense_Unauthenticated_Returns401_ProblemJson()
    {
        // Arrange — client with NO Authorization header
        var client = _factory.CreateClient();

        var fakeGroupId = Guid.NewGuid();
        var fakeExpenseId = Guid.NewGuid();

        var editRequest = new AddExpenseRequest(
            Guid.NewGuid(),
            6000,
            "EUR",
            "Updated description",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>()
        );

        var putContent = System.Text.Json.JsonSerializer.Serialize(editRequest);
        var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/groups/{fakeGroupId}/expenses/{fakeExpenseId}")
        {
            Content = new StringContent(putContent, System.Text.Encoding.UTF8, "application/json")
        };
        putRequest.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue("\"0\""));

        // Act
        var putResponse = await client.SendAsync(putRequest);

        // Assert — 401 Unauthorized (JWT middleware returns empty body, not Problem+JSON)
        putResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EditExpense_InvalidBody_AmountMinorZero_Returns400_ProblemJson()
    {
        // Arrange — register user, create group, add an expense via POST
        var (client, userId) = await CreateAuthClientAsync("edit_expense_invalid@example.com", "InvalidPass123!");
        var createGroupRequest = new CreateGroupRequest("Invalid Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var expenseRequest = new AddExpenseRequest(
            userId,
            6000,
            "EUR",
            "Original description",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );

        var postResponse = await client.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        var createdExpense = await postResponse.ReadJsonAsync<ExpenseDto>();
        createdExpense.Should().NotBeNull();
        var expenseId = createdExpense!.Id;
        var originalVersion = createdExpense.Version;

        // Act — PUT with AmountMinor = 0 (invalid) and correct If-Match header
        var invalidEditRequest = new AddExpenseRequest(
            userId,
            0, // Invalid: AmountMinor must be > 0
            "EUR",
            "Updated description",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );

        var putContent = System.Text.Json.JsonSerializer.Serialize(invalidEditRequest);
        var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/groups/{groupId}/expenses/{expenseId}")
        {
            Content = new StringContent(putContent, System.Text.Encoding.UTF8, "application/json")
        };
        putRequest.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue($"\"{originalVersion}\""));
        var putResponse = await client.SendAsync(putRequest);

        // Assert — 400 Bad Request
        putResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Assert — Problem+JSON body (type, title, status fields present)
        var problemBody = await putResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problemBody.Should().NotBeNull();
        problemBody!.Type.Should().NotBeNullOrEmpty();
        problemBody.Title.Should().NotBeNullOrEmpty();
        problemBody.Status.Should().Be(400);
    }

    [Fact]
    public async Task EditExpense_CurrencyMismatch_Returns400_ProblemJson()
    {
        // Arrange — register user, create group (EUR), add an expense via POST
        var (client, userId) = await CreateAuthClientAsync("edit_expense_currency_mismatch@example.com", "CurrencyMismatch123!");
        var createGroupRequest = new CreateGroupRequest("Currency Mismatch Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var expenseRequest = new AddExpenseRequest(
            userId,
            6000,
            "EUR",
            "Original description",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );

        var postResponse = await client.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        var createdExpense = await postResponse.ReadJsonAsync<ExpenseDto>();
        createdExpense.Should().NotBeNull();
        var expenseId = createdExpense!.Id;
        var originalVersion = createdExpense.Version;

        // Act — PUT with currency "USD" which does NOT match group currency "EUR"
        var mismatchedEditRequest = new AddExpenseRequest(
            userId,
            6000,
            "USD", // Mismatch: group is EUR, request sends USD
            "Updated description",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );

        var putContent = System.Text.Json.JsonSerializer.Serialize(mismatchedEditRequest);
        var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/groups/{groupId}/expenses/{expenseId}")
        {
            Content = new StringContent(putContent, System.Text.Encoding.UTF8, "application/json")
        };
        putRequest.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue($"\"{originalVersion}\""));
        var putResponse = await client.SendAsync(putRequest);

        // Assert — 400 Bad Request
        putResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Assert — Problem+JSON body (type, title, status fields present)
        var problemBody = await putResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problemBody.Should().NotBeNull();
        problemBody!.Type.Should().NotBeNullOrEmpty();
        problemBody.Title.Should().NotBeNullOrEmpty();
        problemBody.Status.Should().Be(400);
    }

    [Fact]
    public async Task EditExpense_PayerNotGroupMember_Returns400_ProblemJson()
    {
        // Arrange — register user A, create group (A is the only member), add an expense
        var (clientA, userIdA) = await CreateAuthClientAsync("edit_expense_payer_owner@example.com", "PayerOwner123!");
        var createGroupRequest = new CreateGroupRequest("Payer Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var expenseRequest = new AddExpenseRequest(
            userIdA,
            6000,
            "EUR",
            "Original description",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, null)
            }
        );

        var postResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        var createdExpense = await postResponse.ReadJsonAsync<ExpenseDto>();
        createdExpense.Should().NotBeNull();
        var expenseId = createdExpense!.Id;
        var originalVersion = createdExpense.Version;

        // Register user B — NOT added to the group
        var (_, userIdB) = await CreateAuthClientAsync("edit_expense_payer_nonmember@example.com", "PayerNonMember123!");

        // Act — user A (who IS a member) tries to edit the expense with payerUserId = user B (NOT a member)
        var editRequest = new AddExpenseRequest(
            userIdB, // Payer is user B — NOT a member of this group
            6000,
            "EUR",
            "Updated description",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, null)
            }
        );

        var putContent = System.Text.Json.JsonSerializer.Serialize(editRequest);
        var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/groups/{groupId}/expenses/{expenseId}")
        {
            Content = new StringContent(putContent, System.Text.Encoding.UTF8, "application/json")
        };
        putRequest.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue($"\"{originalVersion}\""));
        var putResponse = await clientA.SendAsync(putRequest);

        // Assert — 400 Bad Request (payer is not a group member)
        putResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Assert — Problem+JSON body (type, title, status fields present)
        var problemBody = await putResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problemBody.Should().NotBeNull();
        problemBody!.Type.Should().NotBeNullOrEmpty();
        problemBody.Title.Should().NotBeNullOrEmpty();
        problemBody.Status.Should().Be(400);
    }

    [Fact]
    public async Task EditExpense_ChangeSplitMethodFromEqualToExact_RebuildsSplits()
    {
        // Arrange — register two users, create group, add user B
        var (client, userIdA) = await CreateAuthClientAsync("edit_splitmethod_a@example.com", "SplitMethod123!");
        var (_, userIdB) = await RegisterAndLoginAsync("edit_splitmethod_b@example.com", "SplitMethod123!");

        var createGroupRequest = new CreateGroupRequest("Split Method Change Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var addMemberRequest = new SplitBook.Api.Features.Groups.AddMember.AddMemberRequest("edit_splitmethod_b@example.com");
        var addMemberResponse = await client.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.EnsureSuccessStatusCode();

        // Create an Equal-split expense: 6000 EUR, paid by A, split equally between A and B → 3000 each
        var expenseRequest = new AddExpenseRequest(
            userIdA,
            6000,
            "EUR",
            "Dinner",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, null),
                new ExpenseSplitRequest(userIdB, null, null, null)
            }
        );

        var postResponse = await client.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        var createdExpense = await postResponse.ReadJsonAsync<ExpenseDto>();
        createdExpense.Should().NotBeNull();
        var expenseId = createdExpense!.Id;
        var originalVersion = createdExpense.Version;

        // Verify initial state: Equal split, two rows of 3000
        createdExpense.SplitMethod.Should().Be("Equal");
        createdExpense.Splits.Should().HaveCount(2);
        createdExpense.Splits.Single(s => s.UserId == userIdA).AmountMinor.Should().Be(3000);
        createdExpense.Splits.Single(s => s.UserId == userIdB).AmountMinor.Should().Be(3000);

        // Act — edit to Exact split: same total (6000) but A owes 4500, B owes 1500
        var editRequest = new AddExpenseRequest(
            userIdA,
            6000,
            "EUR",
            "Dinner",
            DateOnly.Parse("2024-01-15"),
            "Exact",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, 4500, null, null),
                new ExpenseSplitRequest(userIdB, 1500, null, null)
            }
        );

        var putContent = System.Text.Json.JsonSerializer.Serialize(editRequest);
        var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/groups/{groupId}/expenses/{expenseId}")
        {
            Content = new StringContent(putContent, System.Text.Encoding.UTF8, "application/json")
        };
        putRequest.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue($"\"{originalVersion}\""));
        var putResponse = await client.SendAsync(putRequest);
        var editedExpense = await putResponse.ReadJsonAsync<ExpenseDto>();

        // Assert — 200 OK
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        editedExpense.Should().NotBeNull();

        // Assert — split method changed to Exact
        editedExpense!.SplitMethod.Should().Be("Exact");

        // Assert — ExpenseSplit rows rebuilt with Exact amounts (identify by userId, not positional)
        editedExpense.Splits.Should().HaveCount(2);
        var splitA = editedExpense.Splits.Single(s => s.UserId == userIdA);
        var splitB = editedExpense.Splits.Single(s => s.UserId == userIdB);
        splitA.AmountMinor.Should().Be(4500, "user A exact share should be 4500");
        splitB.AmountMinor.Should().Be(1500, "user B exact share should be 1500");

        // Assert — invariant: split amounts still sum to expense total
        editedExpense.Splits.Sum(s => s.AmountMinor).Should().Be(6000, "split amounts must sum to the expense total");

        // Assert — database ExpenseSplit rows were replaced (not duplicated), with correct Exact amounts
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbSplits = await db.ExpenseSplits
            .Where(es => es.ExpenseId == expenseId)
            .ToListAsync();
        dbSplits.Should().HaveCount(2, "old Equal splits should be replaced by new Exact splits");
        var dbSplitA = dbSplits.Single(es => es.UserId == userIdA);
        var dbSplitB = dbSplits.Single(es => es.UserId == userIdB);
        dbSplitA.AmountMinor.Should().Be(4500);
        dbSplitB.AmountMinor.Should().Be(1500);

        // Assert — database expense row has updated SplitMethod
        var dbExpense = await db.Expenses.SingleAsync(e => e.Id == expenseId);
        dbExpense.SplitMethod.Should().Be(SplitMethod.Exact);
    }

    [Fact]
    public async Task EditExpense_ParticipantNotGroupMember_Returns400_ProblemJson()
    {
        // Arrange — register user A, create group (A is the only member), add an expense for A only
        var (clientA, userIdA) = await CreateAuthClientAsync("edit_expense_participant_owner@example.com", "ParticipantOwner123!");
        var createGroupRequest = new CreateGroupRequest("Participant Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var expenseRequest = new AddExpenseRequest(
            userIdA,
            6000,
            "EUR",
            "Original description",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, null)
            }
        );

        var postResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        var createdExpense = await postResponse.ReadJsonAsync<ExpenseDto>();
        createdExpense.Should().NotBeNull();
        var expenseId = createdExpense!.Id;
        var originalVersion = createdExpense.Version;

        // Register user B — NOT added to the group
        var (_, userIdB) = await CreateAuthClientAsync("edit_expense_participant_nonmember@example.com", "ParticipantNonMember123!");

        // Act — user A (who IS a member) tries to edit the expense with splits including user B (NOT a member)
        var editRequest = new AddExpenseRequest(
            userIdA, // Payer is user A — IS a member
            6000,
            "EUR",
            "Updated description",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, null),
                new ExpenseSplitRequest(userIdB, null, null, null) // user B is NOT a group member
            }
        );

        var putContent = System.Text.Json.JsonSerializer.Serialize(editRequest);
        var putRequest = new HttpRequestMessage(HttpMethod.Put, $"/groups/{groupId}/expenses/{expenseId}")
        {
            Content = new StringContent(putContent, System.Text.Encoding.UTF8, "application/json")
        };
        putRequest.Headers.IfMatch.Add(new System.Net.Http.Headers.EntityTagHeaderValue($"\"{originalVersion}\""));
        var putResponse = await clientA.SendAsync(putRequest);

        // Assert — 400 Bad Request (participant user B is not a group member)
        putResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Assert — Problem+JSON body (type, title, status fields present)
        var problemBody = await putResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problemBody.Should().NotBeNull();
        problemBody!.Type.Should().NotBeNullOrEmpty();
        problemBody.Title.Should().NotBeNullOrEmpty();
        problemBody.Status.Should().Be(400);
    }

 }
