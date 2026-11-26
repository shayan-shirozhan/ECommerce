using ECommerce.Domain.Enums;
using ECommerce.Application.Features.Payments.Shared;

namespace ECommerce.Application.Features.Payments.ResolveAdminPayment;

public sealed record ResolveAdminPaymentResponse(
    Guid AuditId,
    Guid PaymentId,
    Guid OrderId,
    PaymentResolutionAction Action,
    PaymentStatus PaymentStatus,
    OrderStatus OrderStatus,
    long? RefId,
    ZarinPalVerificationOutcome? VerificationOutcome,
    int? RestoredItemCount,
    long? RestoredQuantity,
    DateTime CompletedAtUtc);
