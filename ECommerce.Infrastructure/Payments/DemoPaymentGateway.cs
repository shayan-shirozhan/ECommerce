using ECommerce.Application.Abstractions.Payments;

namespace ECommerce.Infrastructure.Payments;

/// <summary>
/// Deterministic payment simulation for the isolated public Demo environment.
/// This gateway never contacts a payment provider or transfers real funds.
/// </summary>
internal sealed class DemoPaymentGateway : IMockPaymentGateway
{
    public Task<PaymentGatewayResult> ProcessAsync(
        Guid paymentId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (paymentId == Guid.Empty)
            throw new ArgumentException("Payment ID is required.", nameof(paymentId));

        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "Payment amount must be greater than zero.");

        // Stable per-payment transaction IDs make Demo retries and support diagnostics predictable.
        var result = new PaymentGatewayResult(
            true,
            $"demo-txn-{paymentId:N}",
            "public-demo");

        return Task.FromResult(result);
    }
}
