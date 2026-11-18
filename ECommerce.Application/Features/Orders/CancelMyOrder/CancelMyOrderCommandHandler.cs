using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Orders;
using ECommerce.Application.Features.Payments.Shared;
using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Orders.CancelMyOrder;

public sealed class CancelMyOrderCommandHandler(
    ICurrentUser currentUser,
    IOrderRepository orderRepository,
    ISellerOfferRepository sellerOfferRepository,
    IUnitOfWork unitOfWork,
    ZarinPalPaymentVerifier paymentVerifier)
    : IRequestHandler<CancelMyOrderCommand, CancelMyOrderResponse>
{
    public async Task<CancelMyOrderResponse> Handle(
        CancelMyOrderCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Authenticated user is required.");

        var order = await orderRepository.GetByIdForUserForCancellationAsync(
            command.OrderId,
            userId,
            cancellationToken);

        if (order is null)
        {
            throw new NotFoundException(
                $"Order with id '{command.OrderId}' was not found.");
        }

        var pendingZarinPalPayment = order.Payments.SingleOrDefault(payment =>
            payment.PaymentMethod == PaymentMethod.ZarinPal &&
            payment.Status == PaymentStatus.Pending);

        if (pendingZarinPalPayment is not null)
        {
            if (!string.IsNullOrWhiteSpace(pendingZarinPalPayment.Authority))
            {
                // An Authority means a customer may already have completed checkout.
                // Only a server-side, definitive provider rejection makes cancellation
                // safe. Success, an active verification lease, and unknown outcomes
                // must keep both the order and its reserved inventory intact.
                var outcome = await paymentVerifier.VerifyAsync(
                    order,
                    pendingZarinPalPayment,
                    cancellationToken);

                if (outcome != ZarinPalVerificationOutcome.Failed)
                {
                    throw new BusinessRuleException(
                        "The order cannot be cancelled while its ZarinPal payment is not definitively rejected. " +
                        "Please wait for payment reconciliation or contact support if the payment requires manual review.");
                }
            }
            else
            {
                // A checkout-session request may still be completing. Once its lease
                // has elapsed without a persisted Authority, the session URL was never
                // returned to the customer and this local attempt can be cancelled.
                if (!PaymentGatewayRetryPolicy.CanRetry(
                        pendingZarinPalPayment.LastGatewayRequestStartedAt,
                        DateTime.UtcNow))
                {
                    throw new BusinessRuleException(
                        "The ZarinPal checkout request is still being processed. Please retry cancellation in two minutes.");
                }

                pendingZarinPalPayment.Cancel();
            }
        }

        // The domain aggregate still enforces order/group state rules. A ZarinPal
        // payment is now either definitively failed or safely cancelled before stock
        // restoration can begin.
        var restoration = await OrderCancellationProcessor.CancelAndRestoreAsync(
            order,
            sellerOfferRepository,
            cancellationToken);

        // EF Core persists the order, its payments, and all offer stock changes
        // in one database transaction. A concurrency failure rolls everything back.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CancelMyOrderResponse(
            order.Id,
            order.Status,
            restoration.RestoredItemCount,
            restoration.RestoredQuantity);
    }
}
