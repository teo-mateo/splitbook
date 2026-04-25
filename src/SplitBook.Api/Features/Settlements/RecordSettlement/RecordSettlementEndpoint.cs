using Microsoft.AspNetCore.Routing;

namespace SplitBook.Api.Features.Settlements.RecordSettlement;

public static class RecordSettlementEndpoint
{
    public static void MapRecordSettlement(this RouteGroupBuilder group)
    {
        group.MapPost("/{groupId}/settlements", RecordSettlementHandler.HandleAsync);
    }
}
