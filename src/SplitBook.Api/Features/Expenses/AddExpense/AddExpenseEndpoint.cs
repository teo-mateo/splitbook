using Microsoft.AspNetCore.Routing;

namespace SplitBook.Api.Features.Expenses.AddExpense;

public static class AddExpenseEndpoint
{
    public static void MapAddExpense(this RouteGroupBuilder group)
    {
        group.MapPost("/{id}/expenses", AddExpenseHandler.HandleAsync);
    }
}
