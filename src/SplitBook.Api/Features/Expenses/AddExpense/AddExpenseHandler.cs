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
        if (!Enum.TryParse<SplitMethod>(request.SplitMethod, true, out var splitMethod) || splitMethod != SplitMethod.Equal)
        {
            return TypedResults.Problem(title: "Unsupported Split Method", statusCode: 400);
        }

        // Collect all user IDs that need membership checks
        var participantUserIds = request.Splits.Select(s => s.UserId).ToHashSet();
        if (!participantUserIds.Contains(request.PayerUserId))
        {
            participantUserIds.Add(request.PayerUserId);
        }

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

        // Calculate equal split
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

        var splits = CalculateEqualSplit(request, expense.Id);
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
