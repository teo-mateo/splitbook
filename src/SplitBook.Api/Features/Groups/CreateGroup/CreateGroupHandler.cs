using Microsoft.AspNetCore.Http.HttpResults;
using SplitBook.Api.Domain;
using SplitBook.Api.Infrastructure.Auth;
using SplitBook.Api.Infrastructure.Persistence;

namespace SplitBook.Api.Features.Groups.CreateGroup;

public static class CreateGroupHandler
{
    public static async Task<Results<Created<CreateGroupResponse>, ProblemHttpResult>> HandleAsync(
        CreateGroupRequest request,
        HttpContext httpContext,
        CurrentUserAccessor currentUserAccessor,
        AppDbContext context)
    {
        var validator = new CreateGroupValidator();
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
        var userId = currentUser.Id;

        var group = new Group
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Currency = request.Currency.ToUpperInvariant(),
            CreatedByUserId = userId,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        context.Groups.Add(group);

        var membership = new Membership
        {
            GroupId = group.Id,
            UserId = userId,
            JoinedAt = DateTimeOffset.UtcNow,
        };

        context.Memberships.Add(membership);
        await context.SaveChangesAsync();

        return TypedResults.Created(
            $"/groups/{group.Id}",
            new CreateGroupResponse(group.Id, group.Name, group.Currency, group.CreatedAt)
        );
    }
}
