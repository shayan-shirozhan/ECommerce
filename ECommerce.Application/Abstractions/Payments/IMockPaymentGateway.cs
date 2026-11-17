namespace ECommerce.Application.Abstractions.Payments;

/// <summary>
/// Development-only payment processing used by the mock completion endpoint.
/// </summary>
public interface IMockPaymentGateway
{
    Task<PaymentGatewayResult> ProcessAsync(
        Guid paymentId,
        decimal amount,
        CancellationToken cancellationToken = default);
}
