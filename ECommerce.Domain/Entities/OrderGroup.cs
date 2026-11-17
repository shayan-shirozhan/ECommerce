using ECommerce.Domain.Common;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Domain.Entities;

public class OrderGroup : AuditableEntity
{
    //ctor's
    private OrderGroup()
    {

    }

    internal OrderGroup(Guid sellerId)
    {
        if (sellerId == Guid.Empty)
            throw new DomainException("Seller ID is required.");

        SellerId = sellerId;
        Status = OrderGroupStatus.Pending;
    }

    //prop's
    public Guid OrderId { get; private set; }
    public Guid SellerId { get; private set; }
    public OrderGroupStatus Status { get; private set; }
    /// <summary>Shipping carrier chosen by this seller for the group shipment.</summary>
    public string? ShippingCarrier { get; private set; }
    /// <summary>Carrier tracking code for this seller group's shipment.</summary>
    public string? TrackingCode { get; private set; }
    /// <summary>Seller group's total, stored in Toman.</summary>
    public decimal TotalAmount { get; private set; }

    private readonly List<OrderItem> _items = [];
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();


    //method's
    internal void AddItem(OrderItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        _items.Add(item);

        RecalculateTotal();
    }

    private void RecalculateTotal()
    {
        TotalAmount = _items.Sum(x => x.TotalAmount);
    }

    internal void MoveToProcessing()
    {
        if (Status != OrderGroupStatus.Pending)
            throw new DomainException("Order group cannot be moved to processing in its current state.");

        Status = OrderGroupStatus.Processing;
    }

    internal void MoveToShipped(string shippingCarrier, string trackingCode)
    {
        if (Status != OrderGroupStatus.Processing)
            throw new DomainException("Order group cannot be moved to shipped in its current state.");

        if (string.IsNullOrWhiteSpace(shippingCarrier))
            throw new DomainException("Shipping carrier is required when shipping an order group.");

        if (string.IsNullOrWhiteSpace(trackingCode))
            throw new DomainException("Tracking code is required when shipping an order group.");

        var normalizedCarrier = shippingCarrier.Trim();
        var normalizedTrackingCode = trackingCode.Trim();

        if (normalizedCarrier.Length > 100)
            throw new DomainException("Shipping carrier cannot exceed 100 characters.");

        if (normalizedTrackingCode.Length > 100)
            throw new DomainException("Tracking code cannot exceed 100 characters.");

        ShippingCarrier = normalizedCarrier;
        TrackingCode = normalizedTrackingCode;
        Status = OrderGroupStatus.Shipped;
    }

    internal void MoveToDelivered()
    {
        if (Status != OrderGroupStatus.Shipped)
            throw new DomainException("Order group cannot be moved to delivered in its current state.");

        Status = OrderGroupStatus.Delivered;
    }

    internal void Cancel()
    {
        if (Status != OrderGroupStatus.Pending)
            throw new DomainException("Order group cannot be cancelled in its current state.");

        Status = OrderGroupStatus.Cancelled;
    }
}
