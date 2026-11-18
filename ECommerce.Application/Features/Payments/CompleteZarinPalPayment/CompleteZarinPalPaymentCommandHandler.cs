using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Payments.Shared;
using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Payments.CompleteZarinPalPayment;

public sealed class CompleteZarinPalPaymentCommandHandler(
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork,
    ZarinPalPaymentVerifier paymentVerifier)
    : IRequestHandler<CompleteZarinPalPaymentCommand, CompleteZarinPalPaymentResponse>
{
    public async Task<CompleteZarinPalPaymentResponse> Handle(
        CompleteZarinPalPaymentCommand command,
        CancellationToken cancellationToken)
    {
        if (command.PaymentId == Guid.Empty || string.IsNullOrWhiteSpace(command.Authority))
            throw new BusinessRuleException("Payment callback data is invalid.");

        var callbackStatus = command.Status?.Trim();
        if (!string.Equals(callbackStatus, "OK", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(callbackStatus, "NOK", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("Payment callback status is invalid.");
        }

        var order = await orderRepository.GetByPaymentIdForPaymentCallbackAsync(
            command.PaymentId,
            cancellationToken);

        var payment = order?.Payments.SingleOrDefault(candidate => candidate.Id == command.PaymentId);

        if (order is null || payment is null ||
            payment.PaymentMethod != PaymentMethod.ZarinPal ||
            !string.Equals(payment.Authority, command.Authority.Trim(), StringComparison.Ordinal))
        {
            // Do not disclose whether an unrelated payment ID or authority exists.
            throw new NotFoundException("The payment callback does not match a known payment.");
        }

        if (payment.Status == PaymentStatus.Successful)
        {
            return CreateResponse(order.Id, payment, order.Status, isSuccessful: true, alreadyProcessed: true);
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            return CreateResponse(order.Id, payment, order.Status, isSuccessful: false, alreadyProcessed: true);
        }

        if (order.Status != OrderStatus.AwaitingPayment)
            throw new BusinessRuleException("The order is not awaiting payment.");

        if (string.Equals(callbackStatus, "NOK", StringComparison.OrdinalIgnoreCase))
        {
            // A NOK callback must not race with an OK callback or background Verify
            // request that already owns the persisted two-minute verification lease.
            // After any Verify attempt, a later NOK callback is no longer authoritative
            // enough to settle the uncertain result; the server-side worker must Verify again.
            if (payment.VerificationAttemptCount > 0 ||
                !PaymentGatewayRetryPolicy.CanRetry(payment.LastVerificationAttemptedAt, DateTime.UtcNow))
            {
                return CreateResponse(order.Id, payment, order.Status, isSuccessful: false, alreadyProcessed: true);
            }

            // ZarinPal documents an initial NOK callback as a cancelled/unsuccessful
            // attempt and instructs merchants not to call Verify for it. A failed attempt
            // leaves the order open for a new payment attempt.
            payment.MarkFailed();
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // Status=OK from the browser is not proof of payment. The shared verifier
            // checks the saved authority and the amount loaded from our database.
            var outcome = await paymentVerifier.VerifyAsync(order, payment, cancellationToken);
            var alreadyProcessed = outcome == ZarinPalVerificationOutcome.Skipped;

            return CreateResponse(
                order.Id,
                payment,
                order.Status,
                isSuccessful: payment.Status == PaymentStatus.Successful,
                alreadyProcessed);
        }

        return CreateResponse(
            order.Id,
            payment,
            order.Status,
            isSuccessful: payment.Status == PaymentStatus.Successful,
            alreadyProcessed: false);
    }

    private static CompleteZarinPalPaymentResponse CreateResponse(
        Guid orderId,
        ECommerce.Domain.Entities.Payment payment,
        OrderStatus orderStatus,
        bool isSuccessful,
        bool alreadyProcessed)
        => new(
            payment.Id,
            orderId,
            payment.Status,
            orderStatus,
            isSuccessful,
            alreadyProcessed,
            payment.RefId);
}
