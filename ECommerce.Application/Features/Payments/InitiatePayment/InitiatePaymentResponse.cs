using ECommerce.Domain.Enums;

namespace ECommerce.Application.Features.Payments.InitiatePayment;

public sealed record InitiatePaymentResponse(
    Guid PaymentId,
    Guid OrderId,
    decimal Amount,
    PaymentMethod PaymentMethod,
    PaymentStatus Status);
