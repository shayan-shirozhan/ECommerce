using FluentValidation;

namespace ECommerce.Application.Features.Users.ChangeMyPassword;

public sealed class ChangeMyPasswordCommandValidator : AbstractValidator<ChangeMyPasswordCommand>
{
    public ChangeMyPasswordCommandValidator()
    {
        RuleFor(command => command.CurrentPassword)
            .NotEmpty();

        RuleFor(command => command.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .NotEqual(command => command.CurrentPassword)
            .WithMessage("The new password must be different from the current password.");
    }
}
