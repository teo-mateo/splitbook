using Microsoft.AspNetCore.Routing;

namespace SplitBook.Api.Features.Reports.GetUserSummary;

public static class GetUserSummaryEndpoint
{
    public static void MapGetUserSummary(this RouteGroupBuilder group)
    {
        group.MapGet("/me/summary", GetUserSummaryHandler.HandleAsync);
    }
}
