using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SplitBook.Api.Infrastructure.Auth;
using SplitBook.Api.Infrastructure.Persistence;

namespace SplitBook.Api.Features.Groups.GetGroup;

public static class GetGroupHandler
{
    public static async Task<Results<Ok<GroupDetailDto>, ProblemHttpResult>> HandleAsync(
        Guid id,
        HttpContext httpContext,
        AppDbContext db,
        CurrentUserAccessor userAccessor)
    {
        var caller = userAccessor.GetCurrentUser(httpContext);

        var group = await db.Groups
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group == null)
            return TypedResults.Problem(title: "Not Found", statusCode: 404);

        var membership = await db.Memberships
            .FirstOrDefaultAsync(m => m.GroupId == id && m.UserId == caller.Id && m.RemovedAt == null);

        if (membership == null)
            return TypedResults.Problem(title: "Not Found", statusCode: 404);

        var members = await db.Memberships
            .Where(m => m.GroupId == id && m.RemovedAt == null)
            .Join(db.Users, m => m.UserId, u => u.Id, (m, u) => new MemberDto(u.Id, u.DisplayName))
            .ToListAsync();

        var dto = new GroupDetailDto(group.Id, group.Name, group.Currency, group.CreatedAt, group.ArchivedAt, members);

        return TypedResults.Ok(dto);
    }
}
