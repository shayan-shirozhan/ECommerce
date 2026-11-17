using ECommerce.Application.Abstractions.Payments;

namespace ECommerce.Infrastructure.Payments;

internal sealed class MockPaymentGateway : IMockPaymentGateway
{
    public Task<PaymentGatewayResult> ProcessAsync(
        Guid paymentId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var isSuccessful = Random.Shared.Next(0, 100) >= 20;

        var result = isSuccessful
            ? new PaymentGatewayResult(
                true,
                $"mock-txn-{Guid.NewGuid():N}",
                null)
            : new PaymentGatewayResult(false, null, null);

        return Task.FromResult(result);
    }
}
