using FluentValidation;
using Gearify.AuthService.Application.Commands;

namespace Gearify.AuthService.Application.Validators;

/// <summary>
/// Validator for user login command
/// </summary>
public class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required");
    }
}
