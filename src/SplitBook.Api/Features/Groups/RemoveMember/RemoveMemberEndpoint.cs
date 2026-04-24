using Microsoft.AspNetCore.Routing;

namespace SplitBook.Api.Features.Groups.RemoveMember;

public static class RemoveMemberEndpoint
{
    public static void MapRemoveMember(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id}/members/{userId:guid}", RemoveMemberHandler.HandleAsync);
    }
}
