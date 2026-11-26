namespace ECommerce.Application.Abstractions.Payments;

/// <summary>
/// Non-production payment processing used by mock completion endpoints.
/// Implementations are registered only in Development or the isolated Demo environment.
/// </summary>
public interface IMockPaymentGateway
{
    Task<PaymentGatewayResult> ProcessAsync(
        Guid paymentId,
        decimal amount,
        CancellationToken cancellationToken = default);
}
