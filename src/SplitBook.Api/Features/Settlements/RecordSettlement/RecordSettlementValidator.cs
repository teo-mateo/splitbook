using FluentValidation;

namespace SplitBook.Api.Features.Settlements.RecordSettlement;

public class RecordSettlementValidator : AbstractValidator<RecordSettlementRequest>
{
    public RecordSettlementValidator()
    {
        RuleFor(x => x.AmountMinor)
            .GreaterThan(0).WithMessage("Amount must be greater than zero");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required")
            .Length(3).WithMessage("Currency must be exactly 3 characters");

        RuleFor(x => x.FromUserId)
            .NotEmpty().WithMessage("FromUserId is required");

        RuleFor(x => x.ToUserId)
            .NotEmpty().WithMessage("ToUserId is required");
    }
}
