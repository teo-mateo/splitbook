using Microsoft.AspNetCore.Routing;

namespace SplitBook.Api.Features.Expenses.EditExpense;

public static class EditExpenseEndpoint
{
    public static void MapEditExpense(this RouteGroupBuilder group)
    {
        group.MapPut("/{groupId}/expenses/{expenseId}", EditExpenseHandler.HandleAsync);
    }
}
