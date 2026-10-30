using FluentValidation;

namespace ECommerce.Application.Features.Payments.CompleteMockPayment;

public sealed class CompleteMockPaymentCommandValidator
    : AbstractValidator<CompleteMockPaymentCommand>
{
    public CompleteMockPaymentCommandValidator()
    {
        RuleFor(command => command.PaymentId)
            .NotEmpty();
    }
}
