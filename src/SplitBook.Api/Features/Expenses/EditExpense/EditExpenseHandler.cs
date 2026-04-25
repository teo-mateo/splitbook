using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SplitBook.Api.Domain;
using SplitBook.Api.Features.Expenses.AddExpense;
using SplitBook.Api.Infrastructure.Auth;
using SplitBook.Api.Infrastructure.Persistence;

namespace SplitBook.Api.Features.Expenses.EditExpense;

public static class EditExpenseHandler
{
    public static async Task<Results<Ok<ExpenseDto>, ProblemHttpResult>> HandleAsync(
        Guid groupId,
        Guid expenseId,
        AddExpenseRequest request,
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

        // Fetch the expense
        var expense = await context.Expenses
            .FirstOrDefaultAsync(e => e.Id == expenseId && e.GroupId == groupId);
        if (expense == null)
        {
            return TypedResults.Problem(title: "Not Found", statusCode: 404);
        }

        // Check If-Match header for optimistic concurrency
        var ifMatch = httpContext.Request.Headers.IfMatch;
        if (ifMatch.Count == 0)
        {
            return TypedResults.Problem(
                title: "Precondition Failed",
                detail: "If-Match header is required for editing expenses",
                statusCode: 412);
        }

        var etagValue = ifMatch[0];
        if (etagValue == null)
        {
            return TypedResults.Problem(
                title: "Precondition Failed",
                detail: "If-Match header value is invalid",
                statusCode: 412);
        }
        var versionString = etagValue.Trim('"');
        if (!long.TryParse(versionString, out var requestedVersion) || requestedVersion != expense.Version)
        {
            return TypedResults.Problem(
                title: "Precondition Failed",
                detail: "The expense has been modified by another request. Please refresh and try again.",
                statusCode: 412);
        }

        // Fetch group for currency validation
        var group = await context.Groups
            .FirstOrDefaultAsync(g => g.Id == groupId);
        if (group == null)
        {
            return TypedResults.Problem(title: "Not Found", statusCode: 404);
        }

        // Validate request
        var validator = new AddExpenseValidator();
        var validationResult = await validator.ValidateAsync(request);
        if (!validationResult.IsValid)
        {
            return TypedResults.Problem(
                title: "Validation Failed",
                detail: string.Join("; ", validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")),
                statusCode: 400
            );
        }

        // Validate currency matches group currency
        if (request.Currency.ToUpperInvariant() != group.Currency)
        {
            return TypedResults.Problem(
                title: "Currency Mismatch",
                detail: $"Expense currency must match group currency '{group.Currency}'",
                statusCode: 400
            );
        }

        // Parse split method
        if (!Enum.TryParse<SplitMethod>(request.SplitMethod, true, out var splitMethod))
        {
            return TypedResults.Problem(title: "Unsupported Split Method", statusCode: 400);
        }

        // For Exact split, validate that each participant has an amount and that they sum to the total
        if (splitMethod == SplitMethod.Exact)
        {
            foreach (var split in request.Splits)
            {
                if (!split.AmountMinor.HasValue || split.AmountMinor < 0)
                {
                    return TypedResults.Problem(
                        title: "Validation Failed",
                        detail: "Each participant must have a non-negative amountMinor for Exact split",
                        statusCode: 400);
                }
            }

            var splitSum = request.Splits.Sum(s => s.AmountMinor!.Value);
            if (splitSum != request.AmountMinor)
            {
                return TypedResults.Problem(
                    title: "Validation Failed",
                    detail: $"Sum of participant amounts ({splitSum}) must equal the expense total ({request.AmountMinor})",
                    statusCode: 400);
            }
        }

        // For Percentage split, validate that percentages sum to 100 and each is non-negative
        if (splitMethod == SplitMethod.Percentage)
        {
            foreach (var split in request.Splits)
            {
                if (!split.Percentage.HasValue || split.Percentage < 0)
                {
                    return TypedResults.Problem(
                        title: "Validation Failed",
                        detail: "Each participant must have a non-negative percentage for Percentage split",
                        statusCode: 400);
                }
            }

            var percentageSum = request.Splits.Sum(s => s.Percentage!.Value);
            if (Math.Abs(percentageSum - 100.0) > 0.01)
            {
                return TypedResults.Problem(
                    title: "Validation Failed",
                    detail: $"Sum of percentages ({percentageSum}) must equal 100",
                    statusCode: 400);
            }
        }

        // For Shares split, validate that each participant has shares >= 1
        if (splitMethod == SplitMethod.Shares)
        {
            foreach (var split in request.Splits)
            {
                if (!split.Shares.HasValue || split.Shares < 1)
                {
                    return TypedResults.Problem(
                        title: "Validation Failed",
                        detail: "Each participant must have at least 1 share for Shares split",
                        statusCode: 400);
                }
            }
        }

        // Collect all user IDs that need membership checks (always include payer)
        var participantUserIds = request.Splits.Select(s => s.UserId).ToHashSet();
        participantUserIds.Add(request.PayerUserId);

        // Batch-load active memberships for this group and these users
        var activeMemberships = await context.Memberships
            .Where(m => m.GroupId == groupId && participantUserIds.Contains(m.UserId) && m.RemovedAt == null)
            .ToListAsync();

        var activeMemberIds = new HashSet<Guid>(activeMemberships.Select(m => m.UserId));

        // Validate payer is a member
        if (!activeMemberIds.Contains(request.PayerUserId))
        {
            return TypedResults.Problem(title: "Payer is not a member of the group", statusCode: 400);
        }

        // Validate all participants are members
        foreach (var split in request.Splits)
        {
            if (!activeMemberIds.Contains(split.UserId))
            {
                return TypedResults.Problem(title: $"Participant {split.UserId} is not a member of the group", statusCode: 400);
            }
        }

        // Update expense entity
        expense.PayerUserId = request.PayerUserId;
        expense.AmountMinor = request.AmountMinor;
        expense.Currency = group.Currency;
        expense.Description = request.Description;
        expense.OccurredOn = request.OccurredOn;
        expense.SplitMethod = splitMethod;
        expense.Version += 1;

        // Replace ExpenseSplit rows
        var existingSplits = await context.ExpenseSplits
            .Where(es => es.ExpenseId == expenseId)
            .ToListAsync();
        context.ExpenseSplits.RemoveRange(existingSplits);

        var newSplits = splitMethod switch
        {
            SplitMethod.Equal => CalculateEqualSplit(request, expenseId),
            SplitMethod.Exact => CalculateExactSplit(request, expenseId),
            SplitMethod.Percentage => CalculatePercentageSplit(request, expenseId),
            SplitMethod.Shares => CalculateSharesSplit(request, expenseId),
            _ => throw new InvalidOperationException($"Unhandled split method: {splitMethod}")
        };
        context.ExpenseSplits.AddRange(newSplits);

        await context.SaveChangesAsync();

        return TypedResults.Ok(MapToDto(expense, context));
    }

    private static List<ExpenseSplit> CalculateEqualSplit(AddExpenseRequest request, Guid expenseId)
    {
        var participantCount = request.Splits.Count;
        var baseAmount = request.AmountMinor / participantCount;
        var remainder = request.AmountMinor % participantCount;

        var splits = new List<ExpenseSplit>(participantCount);
        for (int i = 0; i < participantCount; i++)
        {
            var participantSplit = request.Splits[i];
            var amount = baseAmount + (i < remainder ? 1 : 0);
            splits.Add(new ExpenseSplit
            {
                ExpenseId = expenseId,
                UserId = participantSplit.UserId,
                AmountMinor = amount,
            });
        }

        return splits;
    }

    private static List<ExpenseSplit> CalculateExactSplit(AddExpenseRequest request, Guid expenseId)
    {
        var splits = new List<ExpenseSplit>(request.Splits.Count);
        foreach (var split in request.Splits)
        {
            splits.Add(new ExpenseSplit
            {
                ExpenseId = expenseId,
                UserId = split.UserId,
                AmountMinor = split.AmountMinor!.Value,
            });
        }

        return splits;
    }

    private static List<ExpenseSplit> CalculatePercentageSplit(AddExpenseRequest request, Guid expenseId)
    {
        var baseAmounts = request.Splits
            .Select(s => (long)Math.Round(s.Percentage!.Value / 100.0 * request.AmountMinor))
            .ToList();
        long assignedTotal = baseAmounts.Sum();
        var remainder = request.AmountMinor - assignedTotal;

        var splits = new List<ExpenseSplit>(request.Splits.Count);
        for (int i = 0; i < request.Splits.Count; i++)
        {
            var split = request.Splits[i];
            var amount = baseAmounts[i] + (i < Math.Abs(remainder) ? (remainder > 0 ? 1 : -1) : 0);

            splits.Add(new ExpenseSplit
            {
                ExpenseId = expenseId,
                UserId = split.UserId,
                AmountMinor = amount,
                Percentage = split.Percentage,
            });
        }

        return splits;
    }

    private static List<ExpenseSplit> CalculateSharesSplit(AddExpenseRequest request, Guid expenseId)
    {
        var totalShares = request.Splits.Sum(s => s.Shares!.Value);

        var baseAmounts = request.Splits.Select(s => (s.Shares!.Value * request.AmountMinor) / totalShares).ToList();
        long assignedTotal = baseAmounts.Sum();
        var remainder = request.AmountMinor - assignedTotal;

        var splits = new List<ExpenseSplit>(request.Splits.Count);
        for (int i = 0; i < request.Splits.Count; i++)
        {
            var split = request.Splits[i];
            var amount = baseAmounts[i] + (i < remainder ? 1 : 0);

            splits.Add(new ExpenseSplit
            {
                ExpenseId = expenseId,
                UserId = split.UserId,
                AmountMinor = amount,
                Shares = split.Shares,
            });
        }

        return splits;
    }

    private static ExpenseDto MapToDto(Expense expense, AppDbContext context)
    {
        var splits = context.ExpenseSplits
            .Where(es => es.ExpenseId == expense.Id)
            .OrderBy(es => es.UserId)
            .Select(es => new ExpenseSplitDto(es.UserId, es.AmountMinor))
            .ToList();

        return new ExpenseDto(
            expense.Id,
            expense.GroupId,
            expense.PayerUserId,
            expense.AmountMinor,
            expense.Currency,
            expense.Description,
            expense.OccurredOn,
            expense.SplitMethod.ToString(),
            splits,
            expense.CreatedAt,
            expense.Version
        );
    }
}
