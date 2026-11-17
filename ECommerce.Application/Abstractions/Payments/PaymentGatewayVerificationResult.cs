namespace ECommerce.Application.Abstractions.Payments;

public sealed record PaymentGatewayVerificationResult(
    bool IsSuccessful,
    bool IsAlreadyVerified,
    long? RefId,
    string? ErrorCode = null,
    string? ErrorMessage = null);
