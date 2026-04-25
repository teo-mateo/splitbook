namespace SplitBook.Api.Features.Expenses.ListExpenses;

using ExpenseDto = SplitBook.Api.Features.Expenses.AddExpense.ExpenseDto;
using ExpenseSplitDto = SplitBook.Api.Features.Expenses.AddExpense.ExpenseSplitDto;

public record ListExpensesResponse(
    List<ExpenseDto> Items,
    int Total
);
