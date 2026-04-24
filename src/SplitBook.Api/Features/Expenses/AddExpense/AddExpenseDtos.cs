namespace SplitBook.Api.Features.Expenses.AddExpense;

public record AddExpenseRequest(
    Guid PayerUserId,
    long AmountMinor,
    string Currency,
    string? Description,
    DateOnly OccurredOn,
    string SplitMethod,
    List<ExpenseSplitRequest> Splits
);

public record ExpenseSplitRequest(
    Guid UserId,
    long? AmountMinor,
    double? Percentage,
    int? Shares
);

public record ExpenseDto(
    Guid Id,
    Guid GroupId,
    Guid PayerUserId,
    long AmountMinor,
    string Currency,
    string? Description,
    DateOnly OccurredOn,
    string SplitMethod,
    List<ExpenseSplitDto> Splits,
    DateTimeOffset CreatedAt
);

public record ExpenseSplitDto(
    Guid UserId,
    long AmountMinor
);
