namespace ECommerce.Application.Abstractions.Payments;

/// <summary>
/// Server-to-server payment operations for a real payment provider.
/// </summary>
public interface IPaymentGateway
{
    string GetPaymentUrl(string authority);

    Task<PaymentGatewayRequestResult> RequestPaymentAsync(
        Guid paymentId,
        Guid orderId,
        decimal amount,
        CancellationToken cancellationToken = default);

    Task<PaymentGatewayVerificationResult> VerifyPaymentAsync(
        decimal amount,
        string authority,
        CancellationToken cancellationToken = default);
}
