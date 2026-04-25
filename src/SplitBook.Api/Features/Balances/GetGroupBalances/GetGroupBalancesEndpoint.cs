using Microsoft.AspNetCore.Routing;

namespace SplitBook.Api.Features.Balances.GetGroupBalances;

public static class GetGroupBalancesEndpoint
{
    public static void MapGetGroupBalances(this RouteGroupBuilder group)
    {
        group.MapGet("/{groupId}/balances", GetGroupBalancesHandler.HandleAsync);
    }
}
