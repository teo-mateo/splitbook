using Microsoft.AspNetCore.Routing;

namespace SplitBook.Api.Features.Groups.ArchiveGroup;

public static class ArchiveGroupEndpoint
{
    public static void MapArchiveGroup(this RouteGroupBuilder group)
    {
        group.MapPost("/{id}/archive", ArchiveGroupHandler.HandleAsync);
    }
}
