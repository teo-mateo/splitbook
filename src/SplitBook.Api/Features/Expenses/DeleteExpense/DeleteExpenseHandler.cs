using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SplitBook.Api.Infrastructure.Auth;
using SplitBook.Api.Infrastructure.Persistence;

namespace SplitBook.Api.Features.Expenses.DeleteExpense;

public static class DeleteExpenseHandler
{
    public static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        Guid groupId,
        Guid expenseId,
        HttpContext httpContext,
        CurrentUserAccessor currentUserAccessor,
        AppDbContext context)
    {
        // Validate caller is a member of the group
        var currentUser = currentUserAccessor.GetCurrentUser(httpContext);
        var membership = await context.Memberships
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == currentUser.Id && m.RemovedAt == null);
        if (membership == null)
        {
            return TypedResults.Problem(title: "Not Found", statusCode: 404);
        }

        // Fetch the expense (global query filter excludes already-deleted rows)
        var expense = await context.Expenses
            .FirstOrDefaultAsync(e => e.Id == expenseId && e.GroupId == groupId);
        if (expense == null)
        {
            return TypedResults.Problem(title: "Not Found", statusCode: 404);
        }

        // Soft delete
        expense.DeletedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync();

        return TypedResults.NoContent();
    }
}
