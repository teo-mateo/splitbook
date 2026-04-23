using Microsoft.AspNetCore.Routing;

namespace SplitBook.Api.Features.Groups.GetGroup;

public static class GetGroupEndpoint
{
    public static void MapGetGroup(this RouteGroupBuilder group)
    {
        group.MapGet("/{id}", GetGroupHandler.HandleAsync);
    }
}
