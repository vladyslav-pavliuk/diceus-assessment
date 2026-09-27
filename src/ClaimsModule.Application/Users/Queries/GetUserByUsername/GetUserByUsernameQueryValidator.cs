using FluentValidation;

namespace ClaimsModule.Application.Users.Queries.GetUserByUsername;

internal sealed class GetUserByUsernameQueryValidator : AbstractValidator<GetUserByUsernameQuery>
{
    public GetUserByUsernameQueryValidator()
    {
        RuleFor(query => query.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MaximumLength(255).WithMessage("Username must not exceed 255 characters.");
    }
}
