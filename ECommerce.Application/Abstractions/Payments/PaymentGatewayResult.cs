namespace ECommerce.Application.Abstractions.Payments;

public sealed record PaymentGatewayResult(
    bool IsSuccessful,
    string? TransactionId,
    string? GatewayReference);
