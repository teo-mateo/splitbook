using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SplitBook.Api.Domain;
using SplitBook.Api.Infrastructure.Auth;
using SplitBook.Api.Infrastructure.Persistence;

namespace SplitBook.Api.Features.Expenses.AddExpense;

public static class AddExpenseHandler
{
    public static async Task<Results<Created<ExpenseDto>, ProblemHttpResult>> HandleAsync(
        Guid id,
        AddExpenseRequest request,
        HttpContext httpContext,
        CurrentUserAccessor currentUserAccessor,
        AppDbContext context)
    {
        // Idempotency check (with 24h window)
        var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            var existing = await context.Expenses
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(e => e.IdempotencyKey == idempotencyKey);
            if (existing != null && existing.CreatedAt > DateTimeOffset.UtcNow.AddHours(-24))
            {
                return MapToCreated(existing, context);
            }
        }

        // Validate caller is a member of the group
        var currentUser = currentUserAccessor.GetCurrentUser(httpContext);
        var membership = await context.Memberships
            .FirstOrDefaultAsync(m => m.GroupId == id && m.UserId == currentUser.Id && m.RemovedAt == null);
        if (membership == null)
        {
            return TypedResults.Problem(title: "Not Found", statusCode: 404);
        }

        // Fetch group for currency validation
        var group = await context.Groups
            .FirstOrDefaultAsync(g => g.Id == id);
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
            .Where(m => m.GroupId == id && participantUserIds.Contains(m.UserId) && m.RemovedAt == null)
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

        // Build expense entity
        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            GroupId = id,
            PayerUserId = request.PayerUserId,
            AmountMinor = request.AmountMinor,
            Currency = group.Currency,
            Description = request.Description,
            OccurredOn = request.OccurredOn,
            SplitMethod = splitMethod,
            CreatedAt = DateTimeOffset.UtcNow,
            IdempotencyKey = idempotencyKey,
        };

        context.Expenses.Add(expense);

        var splits = splitMethod switch
        {
            SplitMethod.Equal => CalculateEqualSplit(request, expense.Id),
            SplitMethod.Exact => CalculateExactSplit(request, expense.Id),
            SplitMethod.Percentage => CalculatePercentageSplit(request, expense.Id),
            SplitMethod.Shares => CalculateSharesSplit(request, expense.Id),
            _ => throw new InvalidOperationException($"Unhandled split method: {splitMethod}")
        };
        context.ExpenseSplits.AddRange(splits);
        await context.SaveChangesAsync();

        return MapToCreated(expense, context);
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
        // Calculate base amounts using rounded percentage of total
        var baseAmounts = request.Splits
            .Select(s => (long)Math.Round(s.Percentage!.Value / 100.0 * request.AmountMinor))
            .ToList();
        long assignedTotal = baseAmounts.Sum();
        var remainder = request.AmountMinor - assignedTotal;

        // Distribute remainder to first N participants (N = remainder)
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

        // Calculate base amounts using integer division
        var baseAmounts = request.Splits.Select(s => (s.Shares!.Value * request.AmountMinor) / totalShares).ToList();
        long assignedTotal = baseAmounts.Sum();
        var remainder = request.AmountMinor - assignedTotal;

        // Distribute remainder to first N participants (N = remainder)
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

    private static Created<ExpenseDto> MapToCreated(Expense expense, AppDbContext context)
    {
        var splits = context.ExpenseSplits
            .Where(es => es.ExpenseId == expense.Id)
            .OrderBy(es => es.UserId)
            .Select(es => new ExpenseSplitDto(es.UserId, es.AmountMinor))
            .ToList();

        var dto = new ExpenseDto(
            expense.Id,
            expense.GroupId,
            expense.PayerUserId,
            expense.AmountMinor,
            expense.Currency,
            expense.Description,
            expense.OccurredOn,
            expense.SplitMethod.ToString(),
            splits,
            expense.CreatedAt
        );

        return TypedResults.Created($"/groups/{expense.GroupId}/expenses/{expense.Id}", dto);
    }
}
