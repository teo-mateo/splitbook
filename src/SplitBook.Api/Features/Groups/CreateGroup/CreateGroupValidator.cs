using FluentValidation;

namespace SplitBook.Api.Features.Groups.CreateGroup;

public class CreateGroupValidator : AbstractValidator<CreateGroupRequest>
{
    public CreateGroupValidator()
    {
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n)).WithMessage("Name must not be empty or whitespace");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Currency is required")
            .Length(3).WithMessage("Currency must be exactly 3 characters")
            .Must(c => c != null && c.All(char.IsLetter)).WithMessage("Currency must be exactly 3 alphabetic characters");
    }
}
