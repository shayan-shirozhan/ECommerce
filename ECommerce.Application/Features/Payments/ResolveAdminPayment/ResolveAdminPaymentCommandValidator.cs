using ECommerce.Domain.Enums;
using FluentValidation;

namespace ECommerce.Application.Features.Payments.ResolveAdminPayment;

public sealed class ResolveAdminPaymentCommandValidator
    : AbstractValidator<ResolveAdminPaymentCommand>
{
    public ResolveAdminPaymentCommandValidator()
    {
        RuleFor(command => command.PaymentId)
            .NotEmpty();

        RuleFor(command => command.Action)
            .IsInEnum();

        RuleFor(command => command.Reason)
            .NotEmpty()
            .MaximumLength(1000);

        When(command => command.Action == PaymentResolutionAction.Approve, () =>
        {
            RuleFor(command => command.RefId)
                .NotNull()
                .GreaterThan(0);
        });

        When(command => command.Action != PaymentResolutionAction.Approve, () =>
        {
            RuleFor(command => command.RefId)
                .Null()
                .WithMessage("RefId is only valid when approving a payment.");
        });
    }
}
