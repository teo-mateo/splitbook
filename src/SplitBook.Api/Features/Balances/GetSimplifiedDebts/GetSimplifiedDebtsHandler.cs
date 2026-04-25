using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SplitBook.Api.Domain;
using SplitBook.Api.Infrastructure.Auth;
using SplitBook.Api.Infrastructure.Persistence;

namespace SplitBook.Api.Features.Balances.GetSimplifiedDebts;

public static class GetSimplifiedDebtsHandler
{
    public static async Task<Results<Ok<List<SimplifiedDebtDto>>, ProblemHttpResult>> HandleAsync(
        Guid groupId,
        HttpContext httpContext,
        CurrentUserAccessor currentUserAccessor,
        AppDbContext context)
    {
        var caller = currentUserAccessor.GetCurrentUser(httpContext);

        var membership = await context.Memberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == caller.Id && m.RemovedAt == null);
        if (membership == null)
        {
            return TypedResults.Problem(title: "Not Found", statusCode: 404);
        }

        var memberIds = await context.Memberships
            .Where(m => m.GroupId == groupId && m.RemovedAt == null)
            .Select(m => m.UserId)
            .ToListAsync();

        var expenses = await context.Expenses
            .Where(e => e.GroupId == groupId)
            .ToListAsync();

        var splits = await context.ExpenseSplits
            .Where(es => expenses.Select(e => e.Id).Contains(es.ExpenseId))
            .ToListAsync();

        var settlements = await context.Settlements
            .Where(s => s.GroupId == groupId)
            .ToListAsync();

        var balances = BalanceCalculator.Calculate(memberIds, expenses, splits, settlements);

        var simplified = DebtSimplifier.Simplify(balances);

        var dtos = simplified.Select(t => new SimplifiedDebtDto(t.FromUserId, t.ToUserId, t.AmountMinor))
            .ToList();

        return TypedResults.Ok(dtos);
    }
}
