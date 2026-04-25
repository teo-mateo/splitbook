using Microsoft.AspNetCore.Routing;

namespace SplitBook.Api.Features.Balances.GetSimplifiedDebts;

public static class GetSimplifiedDebtsEndpoint
{
    public static void MapGetSimplifiedDebts(this RouteGroupBuilder group)
    {
        group.MapGet("/{groupId}/simplified-debts", GetSimplifiedDebtsHandler.HandleAsync);
    }
}
