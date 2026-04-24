using Microsoft.AspNetCore.Routing;

namespace SplitBook.Api.Features.Groups.AddMember;

public static class AddMemberEndpoint
{
    public static void MapAddMember(this RouteGroupBuilder group)
    {
        group.MapPost("/{id}/members", AddMemberHandler.HandleAsync);
    }
}
