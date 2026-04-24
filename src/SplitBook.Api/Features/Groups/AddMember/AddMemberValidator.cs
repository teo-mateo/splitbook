using FluentValidation;

namespace SplitBook.Api.Features.Groups.AddMember;

public class AddMemberValidator : AbstractValidator<AddMemberRequest>
{
    public AddMemberValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address");
    }
}
