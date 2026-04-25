using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SplitBook.Api.Domain;
using SplitBook.Api.Infrastructure.Auth;
using SplitBook.Api.Infrastructure.Persistence;

namespace SplitBook.Api.Features.Reports.GetUserSummary;

public static class GetUserSummaryHandler
{
    public static async Task<Results<Ok<UserSummaryDto>, ProblemHttpResult>> HandleAsync(
        HttpContext httpContext,
        CurrentUserAccessor currentUserAccessor,
        AppDbContext context)
    {
        var caller = currentUserAccessor.GetCurrentUser(httpContext);

        var memberships = await context.Memberships
            .Where(m => m.UserId == caller.Id && m.RemovedAt == null)
            .ToListAsync();

        var groupSummaries = new List<GroupSummaryDto>();

        foreach (var membership in memberships)
        {
            var groupId = membership.GroupId;

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

            var netAmount = balances
                .FirstOrDefault(b => b.UserId == caller.Id)
                .NetAmountMinor;

            var grossAmount = splits
                .Where(s => s.UserId == caller.Id)
                .Sum(s => Math.Abs(s.AmountMinor));

            groupSummaries.Add(new GroupSummaryDto(groupId, netAmount, grossAmount));
        }

        return TypedResults.Ok(new UserSummaryDto(groupSummaries));
    }
}
