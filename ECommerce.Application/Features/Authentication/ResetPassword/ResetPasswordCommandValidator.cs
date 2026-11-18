using FluentValidation;

namespace ECommerce.Application.Features.Authentication.ResetPassword;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(256)
            .EmailAddress();

        RuleFor(command => command.Token)
            .NotEmpty()
            .MaximumLength(4096);

        RuleFor(command => command.NewPassword)
            .NotEmpty();
    }
}
