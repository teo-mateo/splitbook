using FluentValidation;

namespace SplitBook.Api.Features.Auth.Register;

public class RegisterValidator : AbstractValidator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.DisplayName)
            .NotEmpty();

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(6);
    }
}
