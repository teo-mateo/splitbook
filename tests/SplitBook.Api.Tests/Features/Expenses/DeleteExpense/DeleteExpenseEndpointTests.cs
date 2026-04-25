using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SplitBook.Api.Features.Auth.Login;
using SplitBook.Api.Features.Auth.Register;
using SplitBook.Api.Features.Expenses.AddExpense;
using SplitBook.Api.Features.Expenses.ListExpenses;
using SplitBook.Api.Features.Groups.AddMember;
using SplitBook.Api.Features.Groups.CreateGroup;
using SplitBook.Api.Features.Groups.ListMyGroups;
using SplitBook.Api.Infrastructure.Persistence;
using SplitBook.Api.Tests.Infrastructure;
using Xunit;

namespace SplitBook.Api.Tests.Features.Expenses.DeleteExpense;

public class DeleteExpenseEndpointTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public DeleteExpenseEndpointTests(AppFactory factory)
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
    public async Task DeleteExpense_HappyPath_Returns204()
    {
        // Arrange — register user, create group, add an expense via POST
        var (client, userId) = await CreateAuthClientAsync("delete_expense_user@example.com", "DeletePass123!");
        var createGroupRequest = new CreateGroupRequest("Delete Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
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

        var postResponse = await client.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        var createdExpense = await postResponse.ReadJsonAsync<ExpenseDto>();
        createdExpense.Should().NotBeNull();
        var expenseId = createdExpense!.Id;

        // Act — DELETE the expense
        var deleteResponse = await client.DeleteAsync($"/groups/{groupId}/expenses/{expenseId}");

        // Assert — 204 No Content
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteExpense_SoftDelete_SetsDeletedAtAndKeepsRow()
    {
        // Arrange — register user, create group, add an expense via POST
        var (client, userId) = await CreateAuthClientAsync("soft_delete_user@example.com", "SoftDelete123!");
        var createGroupRequest = new CreateGroupRequest("Soft Delete Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var expenseRequest = new AddExpenseRequest(
            userId,
            6000,
            "EUR",
            "Lunch",
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

        // Act — DELETE the expense
        var deleteResponse = await client.DeleteAsync($"/groups/{groupId}/expenses/{expenseId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert — row still exists in database (bypass soft-delete filter via IgnoreQueryFilters)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbExpense = await db.Expenses
            .IgnoreQueryFilters()
            .SingleAsync(e => e.Id == expenseId);

        dbExpense.Should().NotBeNull();
        dbExpense.DeletedAt.Should().NotBeNull("DeletedAt should be set on soft delete");
        dbExpense.DeletedAt!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5), "DeletedAt should be approximately now (UTC)");
    }

    [Fact]
    public async Task DeleteExpense_SoftDelete_RetainsExpenseSplitRows()
    {
        // Arrange — two users, group with both, expense with 2 participants (2 ExpenseSplit rows)
        var (clientA, userIdA) = await CreateAuthClientAsync("split_retain_a@example.com", "Retain123!");
        var (_, userIdB) = await RegisterAndLoginAsync("split_retain_b@example.com", "Retain123!");

        var createGroupRequest = new CreateGroupRequest("Split Retain Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add user B to the group
        var addMemberRequest = new AddMemberRequest("split_retain_b@example.com");
        var addMemberResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/members", addMemberRequest);
        addMemberResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Create expense with 2 participants (Equal split)
        var expenseRequest = new AddExpenseRequest(
            userIdA,
            6000,
            "EUR",
            "Pizza",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userIdA, null, null, null),
                new ExpenseSplitRequest(userIdB, null, null, null)
            }
        );

        var postResponse = await clientA.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        var createdExpense = await postResponse.ReadJsonAsync<ExpenseDto>();
        createdExpense.Should().NotBeNull();
        var expenseId = createdExpense!.Id;

        // Verify 2 ExpenseSplit rows exist before delete
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var splitsBefore = await db.ExpenseSplits.CountAsync(es => es.ExpenseId == expenseId);
            splitsBefore.Should().Be(2, "expense should have 2 ExpenseSplit rows before deletion");
        }

        // Act — DELETE the expense
        var deleteResponse = await clientA.DeleteAsync($"/groups/{groupId}/expenses/{expenseId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert — ExpenseSplit rows still exist (soft delete only affects Expense, not ExpenseSplits)
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var splitsAfter = await db.ExpenseSplits
                .Where(es => es.ExpenseId == expenseId)
                .ToListAsync();

            splitsAfter.Should().HaveCount(2, "ExpenseSplit rows should be retained after soft-deleting the expense");
        }
    }

    [Fact]
    public async Task DeleteExpense_ExcludedFromExpenseList_AfterSoftDelete()
    {
        // Arrange — register user, create group, add an expense via POST
        var (client, userId) = await CreateAuthClientAsync("exclude_list_user@example.com", "ExcludeList123!");
        var createGroupRequest = new CreateGroupRequest("Exclude List Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var expenseRequest = new AddExpenseRequest(
            userId,
            6000,
            "EUR",
            "Coffee",
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

        // Verify the expense appears in GET /groups/{groupId}/expenses (1 item)
        var listBeforeResponse = await client.GetAsync($"/groups/{groupId}/expenses");
        var listBefore = await listBeforeResponse.Content.ReadFromJsonAsync<ListExpensesResponse>();
        listBefore.Should().NotBeNull();
        listBefore!.Items.Should().HaveCount(1);
        listBefore.Total.Should().Be(1);

        // Act — DELETE the expense
        var deleteResponse = await client.DeleteAsync($"/groups/{groupId}/expenses/{expenseId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Assert — GET /groups/{groupId}/expenses returns empty list
        var listAfterResponse = await client.GetAsync($"/groups/{groupId}/expenses");
        var listAfter = await listAfterResponse.Content.ReadFromJsonAsync<ListExpensesResponse>();

        listAfter.Should().NotBeNull();
        listAfter!.Items.Should().BeEmpty("deleted expense must be excluded from the expense list");
        listAfter.Total.Should().Be(0, "deleted expense must not be counted in total");
    }

    [Fact]
    public async Task DeleteExpense_NonExistentExpense_Returns404()
    {
        // Arrange — register user, create group (no expense created)
        var (client, _) = await CreateAuthClientAsync("del_nonexistent_user@example.com", "NonExistent123!");
        var createGroupRequest = new CreateGroupRequest("NonExistent Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var fakeExpenseId = Guid.NewGuid();

        // Act — DELETE a non-existent expense
        var deleteResponse = await client.DeleteAsync($"/groups/{groupId}/expenses/{fakeExpenseId}");

        // Assert — 404 Not Found with Problem+JSON body
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await deleteResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(404);
    }

    [Fact]
    public async Task DeleteExpense_CallerNotGroupMember_Returns404()
    {
        // Arrange — user A creates a group and an expense (A is the only member)
        var (clientA, userIdA) = await CreateAuthClientAsync("del_not_member_a@example.com", "NotMember123!");
        var createGroupRequest = new CreateGroupRequest("Not Member Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var expenseRequest = new AddExpenseRequest(
            userIdA,
            6000,
            "EUR",
            "Dinner",
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

        // User B registers and logs in — but is NOT added to the group
        var (clientB, _) = await CreateAuthClientAsync("del_not_member_b@example.com", "NotMember123!");

        // Act — user B tries to DELETE the expense
        var deleteResponse = await clientB.DeleteAsync($"/groups/{groupId}/expenses/{expenseId}");

        // Assert — 404 Not Found with Problem+JSON body
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await deleteResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(404);
    }

    [Fact]
    public async Task DeleteExpense_AlreadyDeleted_Returns404()
    {
        // Arrange — register user, create group, add an expense via POST
        var (client, userId) = await CreateAuthClientAsync("del_already_deleted_user@example.com", "AlreadyDeleted123!");
        var createGroupRequest = new CreateGroupRequest("Already Deleted Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var expenseRequest = new AddExpenseRequest(
            userId,
            6000,
            "EUR",
            "Snacks",
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

        // First DELETE — should succeed with 204
        var firstDeleteResponse = await client.DeleteAsync($"/groups/{groupId}/expenses/{expenseId}");
        firstDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Act — DELETE the same expense a second time
        var secondDeleteResponse = await client.DeleteAsync($"/groups/{groupId}/expenses/{expenseId}");

        // Assert — 404 Not Found with Problem+JSON body
        secondDeleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await secondDeleteResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(404);
    }

    [Fact]
    public async Task DeleteExpense_Unauthenticated_Returns401()
    {
        // Arrange — client with NO Authorization header
        var client = _factory.CreateClient();

        var fakeGroupId = Guid.NewGuid();
        var fakeExpenseId = Guid.NewGuid();

        // Act — DELETE without a JWT token
        var deleteResponse = await client.DeleteAsync($"/groups/{fakeGroupId}/expenses/{fakeExpenseId}");

        // Assert — 401 Unauthorized
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "an unauthenticated request should receive 401");
    }

    [Fact]
    public async Task DeleteExpense_ExpenseBelongsToDifferentGroup_Returns404()
    {
        // Arrange — register user, create two groups, add expense to group A
        var (client, userId) = await CreateAuthClientAsync("del_diff_group_user@example.com", "DiffGroup123!");

        var createGroupARequest = new CreateGroupRequest("Group A", "EUR");
        var createGroupAResponse = await client.PostAsJsonAsync("/groups", createGroupARequest);
        var groupADto = await createGroupAResponse.ReadJsonAsync<GroupDto>();
        var groupAId = groupADto!.Id;

        var createGroupBRequest = new CreateGroupRequest("Group B", "EUR");
        var createGroupBResponse = await client.PostAsJsonAsync("/groups", createGroupBRequest);
        var groupBDto = await createGroupBResponse.ReadJsonAsync<GroupDto>();
        var groupBId = groupBDto!.Id;

        // Create expense in group A
        var expenseRequest = new AddExpenseRequest(
            userId,
            6000,
            "EUR",
            "Group A Expense",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );

        var postResponse = await client.PostAsJsonAsync($"/groups/{groupAId}/expenses", expenseRequest);
        var createdExpense = await postResponse.ReadJsonAsync<ExpenseDto>();
        createdExpense.Should().NotBeNull();
        var expenseId = createdExpense!.Id;

        // Act — DELETE using group B's ID (expense belongs to group A)
        var deleteResponse = await client.DeleteAsync($"/groups/{groupBId}/expenses/{expenseId}");

        // Assert — 404 Not Found with Problem+JSON body
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await deleteResponse.Content.ReadFromJsonAsync<ProblemDetailsDto>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be(404);
    }
}
