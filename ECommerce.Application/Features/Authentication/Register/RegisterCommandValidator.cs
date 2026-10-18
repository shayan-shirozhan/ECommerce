using FluentValidation;

namespace ECommerce.Application.Features.Authentication.Register;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .MaximumLength(256)
            .EmailAddress();

        RuleFor(x => x.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MinimumLength(8)
            .Must(password => password.Any(char.IsLower))
                .WithMessage("Password must contain at least one lowercase letter.")
            .Must(password => password.Any(char.IsUpper))
                .WithMessage("Password must contain at least one uppercase letter.")
            .Must(password => password.Any(char.IsDigit))
                .WithMessage("Password must contain at least one digit.")
            .Must(password => password.Any(character => !char.IsLetterOrDigit(character)))
                .WithMessage("Password must contain at least one non-alphanumeric character.");

        RuleFor(x => x.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .MaximumLength(100);
    }
}
