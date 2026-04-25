using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SplitBook.Api.Features.Settlements.RecordSettlement;
using SplitBook.Api.Infrastructure.Auth;
using SplitBook.Api.Infrastructure.Persistence;

namespace SplitBook.Api.Features.Settlements.ListSettlements;

public static class ListSettlementsHandler
{
    public static async Task<Results<Ok<List<SettlementDto>>, ProblemHttpResult>> HandleAsync(
        Guid groupId,
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

        var settlements = await context.Settlements
            .Where(s => s.GroupId == groupId)
            .AsNoTracking()
            .ToListAsync();

        // Sort in-memory (L-14: SQLite cannot ORDER BY DateTimeOffset)
        settlements.Sort((a, b) => b.CreatedAt.CompareTo(a.CreatedAt));

        var dtos = settlements.Select(s => new SettlementDto(
            s.Id,
            s.GroupId,
            s.FromUserId,
            s.ToUserId,
            s.AmountMinor,
            s.Currency,
            s.OccurredOn,
            s.CreatedAt
        )).ToList();

        return TypedResults.Ok(dtos);
    }
}
