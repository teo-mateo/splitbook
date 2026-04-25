using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SplitBook.Api.Features.Expenses.AddExpense;
using SplitBook.Api.Infrastructure.Auth;
using SplitBook.Api.Infrastructure.Persistence;

namespace SplitBook.Api.Features.Expenses.ListExpenses;

public static class ListExpensesHandler
{
    public static async Task<Results<Ok<ListExpensesResponse>, ProblemHttpResult>> HandleAsync(
        Guid groupId,
        int? skip,
        int? take,
        DateOnly? from,
        DateOnly? to,
        HttpContext httpContext,
        CurrentUserAccessor currentUserAccessor,
        AppDbContext context)
    {
        var currentUser = currentUserAccessor.GetCurrentUser(httpContext);

        var membership = await context.Memberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == currentUser.Id && m.RemovedAt == null);
        if (membership == null)
        {
            return TypedResults.Problem(title: "Not Found", statusCode: 404);
        }

        var query = context.Expenses
            .Where(e => e.GroupId == groupId);

        if (from.HasValue)
            query = query.Where(e => e.OccurredOn >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.OccurredOn <= to.Value);

        var total = await query.CountAsync();

        var allExpenses = await query
            .AsNoTracking()
            .ToListAsync();

        var expenses = allExpenses
            .OrderByDescending(e => e.OccurredOn)
            .ThenByDescending(e => e.CreatedAt)
            .Skip(skip ?? 0)
            .Take(take ?? 100)
            .ToList();

        var expenseIds = expenses.Select(e => e.Id).ToList();
        var allSplits = await context.ExpenseSplits
            .Where(es => expenseIds.Contains(es.ExpenseId))
            .OrderBy(es => es.ExpenseId)
            .ThenBy(es => es.UserId)
            .AsNoTracking()
            .ToListAsync();

        var splitsByExpenseId = allSplits
            .GroupBy(es => es.ExpenseId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(es => new ExpenseSplitDto(es.UserId, es.AmountMinor)).ToList()
            );

        var items = new List<ExpenseDto>();
        foreach (var expense in expenses)
        {
            var splits = splitsByExpenseId.TryGetValue(expense.Id, out var s) ? s : new List<ExpenseSplitDto>();
            var dto = new ExpenseDto(
                expense.Id,
                expense.GroupId,
                expense.PayerUserId,
                expense.AmountMinor,
                expense.Currency,
                expense.Description,
                expense.OccurredOn,
                expense.SplitMethod.ToString(),
                splits,
                expense.CreatedAt,
                expense.Version
            );
            items.Add(dto);
        }

        return TypedResults.Ok(new ListExpensesResponse(items, total));
    }
}
