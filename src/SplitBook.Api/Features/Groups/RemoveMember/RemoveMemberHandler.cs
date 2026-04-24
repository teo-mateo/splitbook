using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SplitBook.Api.Infrastructure.Auth;
using SplitBook.Api.Infrastructure.Persistence;

namespace SplitBook.Api.Features.Groups.RemoveMember;

public static class RemoveMemberHandler
{
    public static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        Guid id,
        Guid userId,
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

        var callerMembership = await context.Memberships
            .FirstOrDefaultAsync(m => m.GroupId == id && m.UserId == currentUser.Id && m.RemovedAt == null);

        if (callerMembership == null)
        {
            return TypedResults.Problem(title: "Not Found", statusCode: 404);
        }

        var membership = await context.Memberships
            .FirstOrDefaultAsync(m => m.GroupId == id && m.UserId == userId && m.RemovedAt == null);

        if (membership == null)
        {
            return TypedResults.Problem(title: "Not Found", statusCode: 404);
        }

        membership.RemovedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();

        return TypedResults.NoContent();
    }
}
