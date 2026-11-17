namespace ECommerce.Application.Abstractions.Payments;

public sealed record PaymentGatewayRequestResult(
    bool IsSuccessful,
    string? Authority,
    string? PaymentUrl,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    bool IsIndeterminate = false);
