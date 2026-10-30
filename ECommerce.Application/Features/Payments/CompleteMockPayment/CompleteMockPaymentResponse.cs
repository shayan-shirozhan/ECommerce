using ECommerce.Domain.Enums;

namespace ECommerce.Application.Features.Payments.CompleteMockPayment;

public sealed record CompleteMockPaymentResponse(
    Guid PaymentId,
    Guid OrderId,
    PaymentStatus PaymentStatus,
    OrderStatus OrderStatus,
    string? TransactionId,
    string? GatewayReference);
