using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Features.Payments.Shared;
using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Orders.ExpireOrders;

public sealed class ExpireOrderCommandHandler(
    IOrderRepository orderRepository,
    ISellerOfferRepository sellerOfferRepository,
    IUnitOfWork unitOfWork,
    ZarinPalPaymentVerifier paymentVerifier)
    : IRequestHandler<ExpireOrderCommand, bool>
{
    public async Task<bool> Handle(
        ExpireOrderCommand command,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetByIdForExpirationAsync(
            command.OrderId,
            command.CutoffUtc,
            cancellationToken);

        if (order is null)
            return false;

        // An uncertain payment must never be turned into an order cancellation.
        if (order.Payments.Any(payment => payment.Status == PaymentStatus.RequiresManualReview))
            return false;

        var pendingZarinPalRequest = order.Payments.SingleOrDefault(payment =>
            payment.Status == PaymentStatus.Pending &&
            payment.PaymentMethod == PaymentMethod.ZarinPal &&
            string.IsNullOrWhiteSpace(payment.Authority));

        // Do not release reserved stock while a checkout-session request may still be
        // completing. Once its lease expires, there is no Authority to verify and the
        // payment attempt can be expired normally.
        if (pendingZarinPalRequest is not null)
        {
            if (!PaymentGatewayRetryPolicy.CanRetry(
                    pendingZarinPalRequest.LastGatewayRequestStartedAt,
                    DateTime.UtcNow))
            {
                return false;
            }

            // No Authority was persisted and the request lease has expired. The
            // checkout URL was never returned to the customer, so close this local
            // attempt before the domain aggregate permits order cancellation.
            pendingZarinPalRequest.Cancel();
        }

        var pendingZarinPalPayment = order.Payments.SingleOrDefault(payment =>
            payment.Status == PaymentStatus.Pending &&
            payment.PaymentMethod == PaymentMethod.ZarinPal &&
            !string.IsNullOrWhiteSpace(payment.Authority));

        if (pendingZarinPalPayment is not null)
        {
            var outcome = await paymentVerifier.VerifyAsync(
                order,
                pendingZarinPalPayment,
                cancellationToken);

            // Only a definitive provider failure permits expiration to continue.
            // Successful, skipped (active lease), indeterminate, and manual-review
            // outcomes must retain the order and its reserved stock.
            if (outcome != ZarinPalVerificationOutcome.Failed)
                return false;
        }

        await OrderCancellationProcessor.CancelAndRestoreAsync(
            order,
            sellerOfferRepository,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
