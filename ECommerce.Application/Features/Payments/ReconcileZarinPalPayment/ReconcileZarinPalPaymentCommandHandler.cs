using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Features.Payments.Shared;
using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Payments.ReconcileZarinPalPayment;

public sealed class ReconcileZarinPalPaymentCommandHandler(
    IOrderRepository orderRepository,
    ZarinPalPaymentVerifier paymentVerifier)
    : IRequestHandler<ReconcileZarinPalPaymentCommand, ZarinPalVerificationOutcome>
{
    public async Task<ZarinPalVerificationOutcome> Handle(
        ReconcileZarinPalPaymentCommand command,
        CancellationToken cancellationToken)
    {
        if (command.PaymentId == Guid.Empty || string.IsNullOrWhiteSpace(command.Authority))
            return ZarinPalVerificationOutcome.Skipped;

        var order = await orderRepository.GetByPaymentIdForPaymentCallbackAsync(
            command.PaymentId,
            cancellationToken);

        var payment = order?.Payments.SingleOrDefault(candidate => candidate.Id == command.PaymentId);

        if (order is null || payment is null ||
            payment.PaymentMethod != PaymentMethod.ZarinPal ||
            !string.Equals(payment.Authority, command.Authority, StringComparison.Ordinal) ||
            payment.Status != PaymentStatus.Pending ||
            order.Status != OrderStatus.AwaitingPayment)
        {
            return ZarinPalVerificationOutcome.Skipped;
        }

        return await paymentVerifier.VerifyAsync(order, payment, cancellationToken);
    }
}
