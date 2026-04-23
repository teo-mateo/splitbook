using Microsoft.AspNetCore.Routing;

namespace SplitBook.Api.Features.Groups.CreateGroup;

public static class CreateGroupEndpoint
{
    public static void MapCreateGroup(this RouteGroupBuilder group)
    {
        group.MapPost("/", CreateGroupHandler.HandleAsync);
    }
}
