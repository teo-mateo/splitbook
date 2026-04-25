using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using SplitBook.Api.Domain;
using SplitBook.Api.Features.Auth.Login;
using SplitBook.Api.Features.Auth.Register;
using SplitBook.Api.Features.Expenses.AddExpense;
using SplitBook.Api.Features.Expenses.ListExpenses;
using SplitBook.Api.Features.Groups.CreateGroup;
using SplitBook.Api.Features.Groups.ListMyGroups;
using SplitBook.Api.Infrastructure.Persistence;
using SplitBook.Api.Tests.Infrastructure;
using Xunit;

namespace SplitBook.Api.Tests.Features.Expenses.ListExpenses;

public class ListExpensesEndpointTests : IClassFixture<AppFactory>
{
    private readonly AppFactory _factory;

    public ListExpensesEndpointTests(AppFactory factory)
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
    public async Task ListExpenses_HappyPath_Returns200_WithExpensesAndSplits()
    {
        // Arrange — register user, create group, add an expense
        var (client, userId) = await CreateAuthClientAsync("list_expenses_user@example.com", "ListPass123!");

        var createGroupRequest = new CreateGroupRequest("List Expenses Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        var expenseRequest = new AddExpenseRequest(
            userId,
            6000,
            "EUR",
            "Team dinner",
            DateOnly.Parse("2024-06-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );
        var addExpenseResponse = await client.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        addExpenseResponse.EnsureSuccessStatusCode();

        // Act — GET the expense list
        var response = await client.GetAsync($"/groups/{groupId}/expenses");
        var result = await response.Content.ReadFromJsonAsync<ListExpensesResponse>();

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — response shape
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Total.Should().Be(1);

        var expense = result.Items[0];
        expense.Id.Should().NotBe(Guid.Empty);
        expense.PayerUserId.Should().Be(userId);
        expense.AmountMinor.Should().Be(6000);
        expense.Currency.Should().Be("EUR");
        expense.Description.Should().Be("Team dinner");
        expense.OccurredOn.Should().Be(DateOnly.Parse("2024-06-15"));
        expense.SplitMethod.Should().Be("Equal");
        expense.CreatedAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(-5));

        // Assert — splits array
        expense.Splits.Should().HaveCount(1);
        expense.Splits[0].UserId.Should().Be(userId);
        expense.Splits[0].AmountMinor.Should().Be(6000);
    }

    [Fact]
    public async Task ListExpenses_EmptyGroup_Returns200_WithEmptyItemsAndTotal0()
    {
        // Arrange — register user, create group with no expenses
        var (client, _) = await CreateAuthClientAsync("empty_list_user@example.com", "EmptyList123!");

        var createGroupRequest = new CreateGroupRequest("Empty Expenses Group", "USD");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Act — GET expenses for a group that has none
        var response = await client.GetAsync($"/groups/{groupId}/expenses");

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — response shape: empty items, total 0
        var result = await response.Content.ReadFromJsonAsync<ListExpensesResponse>();
        result.Should().NotBeNull();
        result!.Items.Should().BeEmpty();
        result.Total.Should().Be(0);
    }

    [Fact]
    public async Task ListExpenses_Paging_SkipAndTake_LimitsItems_ButTotalReflectsFullCount()
    {
        // Arrange — register user, create group, add 3 expenses
        var (client, userId) = await CreateAuthClientAsync("paging_user@example.com", "PagingPass123!");

        var createGroupRequest = new CreateGroupRequest("Paging Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add 3 expenses
        for (int i = 0; i < 3; i++)
        {
            var expenseRequest = new AddExpenseRequest(
                userId,
                1000 + i * 100,
                "EUR",
                $"Expense {i + 1}",
                DateOnly.Parse("2024-06-15"),
                "Equal",
                new List<ExpenseSplitRequest>
                {
                    new ExpenseSplitRequest(userId, null, null, null)
                }
            );
            var addExpenseResponse = await client.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
            addExpenseResponse.EnsureSuccessStatusCode();
        }

        // Act — GET with skip=1&take=2
        var response = await client.GetAsync($"/groups/{groupId}/expenses?skip=1&take=2");
        var result = await response.Content.ReadFromJsonAsync<ListExpensesResponse>();

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — items array has exactly 2 (take=2), total reflects full count (3)
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(2);
        result.Total.Should().Be(3);
    }

    [Fact]
    public async Task ListExpenses_DateFilter_FromAndTo_ConstrainsByOccurredOn()
    {
        // Arrange — register user, create group, add expenses on different dates
        var (client, userId) = await CreateAuthClientAsync("date_filter_user@example.com", "DateFilter123!");

        var createGroupRequest = new CreateGroupRequest("Date Filter Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Expense in January 2024 (outside the filter range)
        var janExpense = new AddExpenseRequest(
            userId,
            5000,
            "EUR",
            "January expense",
            DateOnly.Parse("2024-01-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );
        await client.PostAsJsonAsync($"/groups/{groupId}/expenses", janExpense);

        // Expense in February 2024 (inside the filter range)
        var febExpense = new AddExpenseRequest(
            userId,
            7500,
            "EUR",
            "February expense",
            DateOnly.Parse("2024-02-20"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );
        await client.PostAsJsonAsync($"/groups/{groupId}/expenses", febExpense);

        // Expense in March 2024 (outside the filter range)
        var marExpense = new AddExpenseRequest(
            userId,
            3000,
            "EUR",
            "March expense",
            DateOnly.Parse("2024-03-10"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );
        await client.PostAsJsonAsync($"/groups/{groupId}/expenses", marExpense);

        // Act — GET with date range filter: February 2024 only
        var response = await client.GetAsync($"/groups/{groupId}/expenses?from=2024-02-01&to=2024-02-28");
        var result = await response.Content.ReadFromJsonAsync<ListExpensesResponse>();

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — only the February expense is returned
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items[0].Description.Should().Be("February expense");
        result.Items[0].OccurredOn.Should().Be(DateOnly.Parse("2024-02-20"));
    }

    [Fact]
    public async Task ListExpenses_NonMemberCaller_Returns404WithProblemJson()
    {
        // Arrange — user A creates a group (auto-added as member)
        var (clientA, _) = await CreateAuthClientAsync("expense_owner_nm@example.com", "OwnerNM123!");

        var createGroupRequest = new CreateGroupRequest("Non-Member Group", "EUR");
        var createGroupResponse = await clientA.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // User B is a separate user — NOT a member of the group
        var (clientB, _) = await CreateAuthClientAsync("stranger_nm@example.com", "StrangerNM123!");

        // Act — user B tries to list expenses for a group they don't belong to
        var response = await clientB.GetAsync($"/groups/{groupId}/expenses");
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
    public async Task ListExpenses_Unauthenticated_Returns401()
    {
        // Arrange — client with no Authorization header
        var client = _factory.CreateClient();

        // Act — GET expenses without a JWT token
        var response = await client.GetAsync($"/groups/{Guid.NewGuid()}/expenses");

        // Assert — HTTP 401 Unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "an unauthenticated request should receive 401");
    }

    [Fact]
    public async Task ListExpenses_SoftDeletedExpense_ExcludedFromListAndTotal()
    {
        // Arrange — register user, create group, add one expense via POST
        var (client, userId) = await CreateAuthClientAsync("soft_delete_user@example.com", "SoftDel123!");

        var createGroupRequest = new CreateGroupRequest("Soft Delete Group", "EUR");
        var createGroupResponse = await client.PostAsJsonAsync("/groups", createGroupRequest);
        var groupDto = await createGroupResponse.ReadJsonAsync<GroupDto>();
        var groupId = groupDto!.Id;

        // Add one live expense via the API
        var expenseRequest = new AddExpenseRequest(
            userId,
            6000,
            "EUR",
            "Live expense",
            DateOnly.Parse("2024-06-15"),
            "Equal",
            new List<ExpenseSplitRequest>
            {
                new ExpenseSplitRequest(userId, null, null, null)
            }
        );
        var addExpenseResponse = await client.PostAsJsonAsync($"/groups/{groupId}/expenses", expenseRequest);
        addExpenseResponse.EnsureSuccessStatusCode();

        // Seed a second expense directly in the DB with DeletedAt set (simulating a soft-deleted expense)
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deletedExpense = new Expense
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            PayerUserId = userId,
            AmountMinor = 3000,
            Currency = "EUR",
            Description = "Deleted expense",
            OccurredOn = DateOnly.Parse("2024-06-16"),
            SplitMethod = SplitMethod.Equal,
            CreatedAt = DateTimeOffset.UtcNow,
            DeletedAt = DateTimeOffset.UtcNow,
            Version = 1,
            ExpenseSplits = new List<ExpenseSplit>
            {
                new ExpenseSplit
                {
                    ExpenseId = Guid.Empty, // will be set after Add
                    UserId = userId,
                    AmountMinor = 3000
                }
            }
        };
        db.Expenses.Add(deletedExpense);
        // Fix up the ExpenseSplit.ExpenseId after Add (EF generates the navigation)
        deletedExpense.ExpenseSplits.First().ExpenseId = deletedExpense.Id;
        await db.SaveChangesAsync();

        // Act — GET the expense list
        var response = await client.GetAsync($"/groups/{groupId}/expenses");
        var result = await response.Content.ReadFromJsonAsync<ListExpensesResponse>();

        // Assert — HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — only the live expense appears; deleted one is excluded
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1, "soft-deleted expenses must be excluded from the list");
        result.Items[0].Description.Should().Be("Live expense");
        result.Total.Should().Be(1, "soft-deleted expenses must not be counted in total");
    }
}
