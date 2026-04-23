using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SplitBook.Api.Infrastructure.Auth;
using SplitBook.Api.Infrastructure.Persistence;

namespace SplitBook.Api.Features.Groups.ListMyGroups;

public static class ListMyGroupsHandler
{
    public static async Task<Ok<List<GroupDto>>> HandleAsync(
        HttpContext httpContext,
        CurrentUserAccessor currentUserAccessor,
        AppDbContext context)
    {
        var currentUser = currentUserAccessor.GetCurrentUser(httpContext);
        var userId = currentUser.Id;

        var groups = await context.Memberships
            .Where(m => m.UserId == userId && m.RemovedAt == null)
            .Join(
                context.Groups.Where(g => g.ArchivedAt == null),
                m => m.GroupId,
                g => g.Id,
                (m, g) => new GroupDto(g.Id, g.Name, g.Currency, g.CreatedAt))
            .AsNoTracking()
            .ToListAsync();

        return TypedResults.Ok(groups);
    }
}
