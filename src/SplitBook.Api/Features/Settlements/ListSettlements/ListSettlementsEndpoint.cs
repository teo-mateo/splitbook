using Microsoft.AspNetCore.Routing;

namespace SplitBook.Api.Features.Settlements.ListSettlements;

public static class ListSettlementsEndpoint
{
    public static void MapListSettlements(this RouteGroupBuilder group)
    {
        group.MapGet("/{groupId}/settlements", ListSettlementsHandler.HandleAsync);
    }
}
