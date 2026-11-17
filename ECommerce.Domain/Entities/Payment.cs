using ECommerce.Domain.Common;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Domain.Entities;

public class Payment : AuditableEntity
{
    private Payment()
    {
    }

    private Payment(Guid orderId, decimal amount, PaymentMethod paymentMethod)
    {
        if (orderId == Guid.Empty)
            throw new DomainException("Order ID is required.");

        if (amount <= 0)
            throw new DomainException("Payment amount must be greater than zero.");

        OrderId = orderId;
        Amount = amount;
        PaymentMethod = paymentMethod;
        Status = PaymentStatus.Pending;
    }

    public Guid OrderId { get; private set; }
    public uint RowVersion { get; private set; }
    /// <summary>Payment amount, stored in Toman.</summary>
    public decimal Amount { get; private set; }
    public PaymentStatus Status { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; }
    public string? Authority { get; private set; }
    public long? RefId { get; private set; }
    public string? TransactionId { get; private set; }
    public string? GatewayReference { get; private set; }
    public DateTime? PaidAt { get; private set; }
    public DateTime? FailedAt { get; private set; }

    public static Payment Create(Guid orderId, decimal amount, PaymentMethod paymentMethod)
    {
        return new Payment(orderId, amount, paymentMethod);
    }

    public void SetAuthority(string authority)
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException("Only pending payments can receive a gateway authority.");

        if (string.IsNullOrWhiteSpace(authority))
            throw new DomainException("Payment authority is required.");

        if (!string.IsNullOrWhiteSpace(Authority))
            throw new DomainException("Payment authority has already been assigned.");

        Authority = authority.Trim();
    }

    public void MarkSuccessful(
        string? transactionId = null,
        string? gatewayReference = null,
        long? refId = null)
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException("Only pending payment can be marked as successful.");

        if (string.IsNullOrWhiteSpace(transactionId) &&
            string.IsNullOrWhiteSpace(gatewayReference) &&
            !refId.HasValue)
        {
            throw new DomainException("A payment reference is required.");
        }

        if (refId is <= 0)
            throw new DomainException("Payment reference ID must be greater than zero.");

        Status = PaymentStatus.Successful;
        TransactionId = string.IsNullOrWhiteSpace(transactionId) ? null : transactionId.Trim();
        GatewayReference = string.IsNullOrWhiteSpace(gatewayReference) ? null : gatewayReference.Trim();
        RefId = refId;
        PaidAt = DateTime.UtcNow;
    }

    public void MarkFailed()
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException("Only pending payment can be marked as failed.");

        Status = PaymentStatus.Failed;
        FailedAt = DateTime.UtcNow;
    }

    public void Cancel()
    {
        if (Status != PaymentStatus.Pending)
            throw new DomainException("Only pending payments can be cancelled.");

        Status = PaymentStatus.Cancelled;
    }
}
