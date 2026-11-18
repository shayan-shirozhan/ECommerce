using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Options;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.Extensions.Options;

namespace ECommerce.Application.Features.Payments.Shared;

/// <summary>
/// Executes the server-to-server verification workflow shared by browser callbacks,
/// the expiration process, and the periodic reconciliation worker.
/// </summary>
public sealed class ZarinPalPaymentVerifier(
    IEnumerable<IPaymentGateway> paymentGateways,
    IUnitOfWork unitOfWork,
    IOptions<OrderExpirationOptions> expirationOptions)
{
    public async Task<ZarinPalVerificationOutcome> VerifyAsync(
        Order order,
        Payment payment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(payment);

        if (payment.PaymentMethod != PaymentMethod.ZarinPal ||
            payment.Status != PaymentStatus.Pending ||
            string.IsNullOrWhiteSpace(payment.Authority) ||
            order.Status != OrderStatus.AwaitingPayment)
        {
            return ZarinPalVerificationOutcome.Skipped;
        }

        var nowUtc = DateTime.UtcNow;

        // A callback or another worker may already be inside the provider request.
        // The persisted timestamp acts as a lease across processes and application instances.
        if (!PaymentGatewayRetryPolicy.CanRetry(payment.LastVerificationAttemptedAt, nowUtc))
            return ZarinPalVerificationOutcome.Skipped;

        var limits = expirationOptions.Value;
        var maximumAge = TimeSpan.FromMinutes(limits.MaxVerificationAgeMinutes);

        if (payment.HasReachedVerificationLimit(
                nowUtc,
                limits.MaxVerificationAttempts,
                maximumAge))
        {
            payment.MarkRequiresManualReview();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ZarinPalVerificationOutcome.RequiresManualReview;
        }

        var paymentGateway = paymentGateways.SingleOrDefault()
            ?? throw new PaymentGatewayException("ZarinPal payment gateway is not configured.");

        // Save the attempt and lease before making the remote call. If the process exits
        // after this point, the next worker can safely resume once the lease has elapsed.
        payment.MarkVerificationAttempted(nowUtc);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var verification = await paymentGateway.VerifyPaymentAsync(
            payment.Amount,
            payment.Authority!,
            cancellationToken);

        if (verification.IsSuccessful && verification.RefId is > 0)
        {
            payment.MarkSuccessful(refId: verification.RefId.Value);
            order.Confirm();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ZarinPalVerificationOutcome.Successful;
        }

        if (verification.IsSuccessful)
        {
            // A provider success without a usable reference cannot be treated as either
            // a confirmed payment or a definitive failure.
            payment.RecordVerificationOutcome("SUCCESS_WITHOUT_REF_ID");
            if (payment.HasReachedVerificationLimit(
                    DateTime.UtcNow,
                    limits.MaxVerificationAttempts,
                    maximumAge))
            {
                payment.MarkRequiresManualReview();
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ZarinPalVerificationOutcome.RequiresManualReview;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ZarinPalVerificationOutcome.Indeterminate;
        }

        payment.RecordVerificationOutcome(verification.ErrorCode);

        if (verification.IsIndeterminate)
        {
            if (payment.HasReachedVerificationLimit(
                    DateTime.UtcNow,
                    limits.MaxVerificationAttempts,
                    maximumAge))
            {
                payment.MarkRequiresManualReview();
                await unitOfWork.SaveChangesAsync(cancellationToken);
                return ZarinPalVerificationOutcome.RequiresManualReview;
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
            return ZarinPalVerificationOutcome.Indeterminate;
        }

        // The gateway currently classifies only Verify code -51 as definitive failure.
        payment.MarkFailed();
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ZarinPalVerificationOutcome.Failed;
    }
}
