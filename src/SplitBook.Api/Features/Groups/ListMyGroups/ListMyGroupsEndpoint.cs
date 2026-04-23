using Microsoft.AspNetCore.Routing;

namespace SplitBook.Api.Features.Groups.ListMyGroups;

public static class ListMyGroupsEndpoint
{
    public static void MapListMyGroups(this RouteGroupBuilder group)
    {
        group.MapGet("/", ListMyGroupsHandler.HandleAsync);
    }
}
