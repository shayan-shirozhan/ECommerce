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

    internal void MoveToShipped()
    {
        if (Status != OrderGroupStatus.Processing)
            throw new DomainException("Order group cannot be moved to shipped in its current state.");

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
