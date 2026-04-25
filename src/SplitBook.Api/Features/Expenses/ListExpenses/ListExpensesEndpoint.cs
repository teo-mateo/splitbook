using Microsoft.AspNetCore.Routing;

namespace SplitBook.Api.Features.Expenses.ListExpenses;

public static class ListExpensesEndpoint
{
    public static void MapListExpenses(this RouteGroupBuilder group)
    {
        group.MapGet("/{groupId}/expenses", ListExpensesHandler.HandleAsync);
    }
}
