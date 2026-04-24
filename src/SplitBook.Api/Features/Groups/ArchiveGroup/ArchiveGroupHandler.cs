using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SplitBook.Api.Infrastructure.Auth;
using SplitBook.Api.Infrastructure.Persistence;

namespace SplitBook.Api.Features.Groups.ArchiveGroup;

public static class ArchiveGroupHandler
{
    public static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        Guid id,
        HttpContext httpContext,
        CurrentUserAccessor currentUserAccessor,
        AppDbContext context)
    {
        var currentUser = currentUserAccessor.GetCurrentUser(httpContext);

        var group = await context.Groups
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group == null)
        {
            return TypedResults.Problem(title: "Not Found", statusCode: 404);
        }

        var membership = await context.Memberships
            .FirstOrDefaultAsync(m => m.GroupId == id && m.UserId == currentUser.Id && m.RemovedAt == null);

        if (membership == null)
        {
            return TypedResults.Problem(title: "Not Found", statusCode: 404);
        }

        // NOTE: product-spec §5 says archive is the escape hatch for groups with
        // non-zero balances ("cannot be deleted; it can be archived"). The
        // technical-spec §4 and slice-plan originally said "fails if any non-zero
        // balance" — this contradicts the product spec. We follow the product spec:
        // archive succeeds unconditionally. A balance guard can be added later if
        // the human revises the product spec.
        group.ArchivedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();

        return TypedResults.NoContent();
    }
}
