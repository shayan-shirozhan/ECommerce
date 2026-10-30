namespace ECommerce.Application.Abstractions.Payments;

public interface IPaymentGateway
{
    /// <summary>
    /// Processes the payment. Implementations backed by a real provider must use
    /// paymentId as the provider's idempotency key so retries cannot charge twice.
    /// </summary>
    Task<PaymentGatewayResult> ProcessAsync(
        Guid paymentId,
        decimal amount,
        CancellationToken cancellationToken = default);
}
