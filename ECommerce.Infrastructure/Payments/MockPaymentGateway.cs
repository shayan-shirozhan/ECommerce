using ECommerce.Application.Abstractions.Payments;

namespace ECommerce.Infrastructure.Payments;

internal sealed class MockPaymentGateway : IPaymentGateway
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

        // Mock-only behavior: 80% success and 20% failure.
        var isSuccessful = Random.Shared.Next(0, 100) >= 20;

        var result = isSuccessful
            ? new PaymentGatewayResult(
                true,
                $"MOCK-TXN-{Guid.NewGuid():N}",
                $"MOCK-REF-{paymentId:N}")
            : new PaymentGatewayResult(false, null, null);

        return Task.FromResult(result);
    }
}
