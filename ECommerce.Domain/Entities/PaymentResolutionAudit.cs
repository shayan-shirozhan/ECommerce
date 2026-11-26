using ECommerce.Domain.Common;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Domain.Entities;

public class PaymentResolutionAudit : AuditableEntity
{
    private PaymentResolutionAudit()
    {
    }

    private PaymentResolutionAudit(
        Guid paymentId,
        Guid orderId,
        Guid adminUserId,
        PaymentResolutionAction action,
        string reason,
        long? refId)
    {
        if (paymentId == Guid.Empty)
            throw new DomainException("Payment ID is required.");

        if (orderId == Guid.Empty)
            throw new DomainException("Order ID is required.");

        if (adminUserId == Guid.Empty)
            throw new DomainException("Administrator ID is required.");

        if (!Enum.IsDefined(action))
            throw new DomainException("Payment resolution action is invalid.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("A reason is required for payment resolution.");

        var normalizedReason = reason.Trim();
        if (normalizedReason.Length > 1000)
            throw new DomainException("Payment resolution reason cannot exceed 1000 characters.");

        if (refId is <= 0)
            throw new DomainException("Payment reference ID must be greater than zero.");

        if (action == PaymentResolutionAction.Approve && !refId.HasValue)
            throw new DomainException("A payment reference ID is required when approving a payment.");

        if (action != PaymentResolutionAction.Approve && refId.HasValue)
            throw new DomainException("A payment reference ID is only valid when approving a payment.");

        PaymentId = paymentId;
        OrderId = orderId;
        AdminUserId = adminUserId;
        Action = action;
        Reason = normalizedReason;
        RefId = refId;
    }

    public Guid PaymentId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid AdminUserId { get; private set; }
    public PaymentResolutionAction Action { get; private set; }
    public string Reason { get; private set; } = null!;
    public long? RefId { get; private set; }
    public string? Outcome { get; private set; }
    public string? ProviderErrorCode { get; private set; }
    public PaymentStatus? PaymentStatusAfter { get; private set; }
    public OrderStatus? OrderStatusAfter { get; private set; }
    public int? RestoredItemCount { get; private set; }
    public long? RestoredQuantity { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }

    public static PaymentResolutionAudit Create(
        Guid paymentId,
        Guid orderId,
        Guid adminUserId,
        PaymentResolutionAction action,
        string reason,
        long? refId = null)
        => new(paymentId, orderId, adminUserId, action, reason, refId);

    public void Complete(
        string outcome,
        PaymentStatus paymentStatusAfter,
        OrderStatus orderStatusAfter,
        int? restoredItemCount = null,
        long? restoredQuantity = null,
        DateTime? completedAtUtc = null,
        long? resolvedRefId = null,
        string? providerErrorCode = null)
    {
        if (CompletedAtUtc.HasValue)
            throw new DomainException("Payment resolution audit is already completed.");

        if (string.IsNullOrWhiteSpace(outcome))
            throw new DomainException("Payment resolution outcome is required.");

        if (outcome.Length > 100)
            throw new DomainException("Payment resolution outcome cannot exceed 100 characters.");

        if (restoredItemCount is < 0 || restoredQuantity is < 0)
            throw new DomainException("Restored inventory values cannot be negative.");

        if (resolvedRefId is <= 0)
            throw new DomainException("Payment reference ID must be greater than zero.");

        if (RefId.HasValue && resolvedRefId.HasValue && RefId != resolvedRefId)
            throw new DomainException("Payment reference ID cannot be changed after the audit is created.");

        var normalizedProviderErrorCode = providerErrorCode?.Trim();
        if (normalizedProviderErrorCode is { Length: > 50 })
            throw new DomainException("Provider error code cannot exceed 50 characters.");

        var completedAt = completedAtUtc ?? DateTime.UtcNow;
        if (completedAt.Kind != DateTimeKind.Utc)
            throw new DomainException("Payment resolution completion time must be UTC.");

        Outcome = outcome.Trim();
        RefId ??= resolvedRefId;
        ProviderErrorCode = string.IsNullOrEmpty(normalizedProviderErrorCode)
            ? null
            : normalizedProviderErrorCode;
        PaymentStatusAfter = paymentStatusAfter;
        OrderStatusAfter = orderStatusAfter;
        RestoredItemCount = restoredItemCount;
        RestoredQuantity = restoredQuantity;
        CompletedAtUtc = completedAt;
    }
}
