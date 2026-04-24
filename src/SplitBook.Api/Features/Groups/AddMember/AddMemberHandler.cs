using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SplitBook.Api.Domain;
using SplitBook.Api.Infrastructure.Auth;
using SplitBook.Api.Infrastructure.Persistence;

namespace SplitBook.Api.Features.Groups.AddMember;

public static class AddMemberHandler
{
    public static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        Guid id,
        AddMemberRequest request,
        HttpContext httpContext,
        CurrentUserAccessor currentUserAccessor,
        AppDbContext context)
    {
        var validator = new AddMemberValidator();
        var result = await validator.ValidateAsync(request);
        if (!result.IsValid)
        {
            return TypedResults.Problem(
                title: "Validation Failed",
                detail: string.Join("; ", result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")),
                statusCode: 400
            );
        }

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

        var targetUser = await context.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant());

        if (targetUser == null)
        {
            return TypedResults.Problem(title: "Not Found", detail: "User not found", statusCode: 404);
        }

        var existingMembership = await context.Memberships
            .FirstOrDefaultAsync(m => m.GroupId == id && m.UserId == targetUser.Id && m.RemovedAt == null);

        if (existingMembership != null)
        {
            return TypedResults.Problem(title: "Conflict", detail: "User is already a member", statusCode: 409);
        }

        var membership = new Membership
        {
            GroupId = group.Id,
            UserId = targetUser.Id,
            JoinedAt = DateTimeOffset.UtcNow,
        };

        context.Memberships.Add(membership);
        await context.SaveChangesAsync();

        return TypedResults.NoContent();
    }
}
