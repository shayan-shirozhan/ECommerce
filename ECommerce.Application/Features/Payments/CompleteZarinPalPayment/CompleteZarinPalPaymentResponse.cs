using ECommerce.Domain.Enums;

namespace ECommerce.Application.Features.Payments.CompleteZarinPalPayment;

public sealed record CompleteZarinPalPaymentResponse(
    Guid PaymentId,
    Guid OrderId,
    PaymentStatus PaymentStatus,
    OrderStatus OrderStatus,
    bool IsSuccessful,
    bool WasAlreadyProcessed,
    long? RefId);
