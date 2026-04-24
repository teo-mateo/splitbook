using FluentValidation;

namespace SplitBook.Api.Features.Expenses.AddExpense;

public class AddExpenseValidator : AbstractValidator<AddExpenseRequest>
{
    public AddExpenseValidator()
    {
        RuleFor(x => x.AmountMinor)
            .GreaterThan(0).WithMessage("Amount must be greater than zero");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required")
            .Length(3).WithMessage("Currency must be exactly 3 characters");

        RuleFor(x => x.Splits)
            .NotEmpty().WithMessage("At least one participant is required");

        RuleFor(x => x.SplitMethod)
            .NotEmpty().WithMessage("Split method is required");
    }
}
