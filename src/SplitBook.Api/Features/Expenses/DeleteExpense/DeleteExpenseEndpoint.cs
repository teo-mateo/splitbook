using Microsoft.AspNetCore.Routing;

namespace SplitBook.Api.Features.Expenses.DeleteExpense;

public static class DeleteExpenseEndpoint
{
    public static void MapDeleteExpense(this RouteGroupBuilder group)
    {
        group.MapDelete("/{groupId}/expenses/{expenseId}", DeleteExpenseHandler.HandleAsync);
    }
}
