using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Orders.ExpireOrders;

public sealed class ExpireOrderCommandHandler(
    IOrderRepository orderRepository,
    ISellerOfferRepository sellerOfferRepository,
    IEnumerable<IPaymentGateway> paymentGateways,
    IUnitOfWork unitOfWork)
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

        var pendingZarinPalPayment = order.Payments.SingleOrDefault(payment =>
            payment.Status == PaymentStatus.Pending &&
            payment.PaymentMethod == PaymentMethod.ZarinPal &&
            !string.IsNullOrWhiteSpace(payment.Authority));

        if (pendingZarinPalPayment is not null)
        {
            if (!PaymentGatewayRetryPolicy.CanRetry(
                    pendingZarinPalPayment.LastVerificationAttemptedAt,
                    DateTime.UtcNow))
            {
                return false;
            }

            // Persist the reconciliation lease before resolving/calling the gateway.
            // This also prevents a missing or misconfigured gateway from letting the
            // same oldest orders monopolize every background-service batch.
            pendingZarinPalPayment.MarkVerificationAttempted(DateTime.UtcNow);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var paymentGateway = paymentGateways.SingleOrDefault()
                ?? throw new PaymentGatewayException(
                    "ZarinPal is required to reconcile this pending payment before its order can expire.");

            var verification = await paymentGateway.VerifyPaymentAsync(
                pendingZarinPalPayment.Amount,
                pendingZarinPalPayment.Authority!,
                cancellationToken);

            if (verification.IsSuccessful)
            {
                if (verification.RefId is not > 0)
                {
                    throw new PaymentGatewayException(
                        "ZarinPal reported a successful verification without a valid reference ID. " +
                        "The order remains awaiting reconciliation.");
                }

                pendingZarinPalPayment.MarkSuccessful(refId: verification.RefId.Value);
                order.Confirm();
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return false;
            }

            if (verification.IsIndeterminate)
            {
                // Keep both the order and reserved stock intact until the provider
                // confirms success or a definitive failure can be established.
                throw new PaymentGatewayException(
                    "ZarinPal could not determine the payment outcome. The order remains pending reconciliation.");
            }

            pendingZarinPalPayment.MarkFailed();
        }

        await OrderCancellationProcessor.CancelAndRestoreAsync(
            order,
            sellerOfferRepository,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
