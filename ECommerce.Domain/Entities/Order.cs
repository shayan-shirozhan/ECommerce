using ECommerce.Domain.Common;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Domain.Entities;

public class Order : AuditableEntity
{
    //ctor's
    private Order()
    {

    }

    private Order(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new DomainException("User ID is required.");

        UserId = userId;
        Status = OrderStatus.Pending;
    }


    //prop's
    public Guid UserId { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string ShippingRecipientName { get; private set; } = null!;
    public string ShippingPhoneNumber { get; private set; } = null!;
    public string ShippingProvince { get; private set; } = null!;
    public string ShippingCity { get; private set; } = null!;
    public string ShippingPostalCode { get; private set; } = null!;
    public string ShippingAddressLine { get; private set; } = null!;

    private readonly List<OrderGroup> _groups = [];
    public IReadOnlyCollection<OrderGroup> Groups => _groups.AsReadOnly();
    private readonly List<Payment> _payments = [];
    public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();


    //method's
    public static Order Create(Guid userId, string recipientName, string phoneNumber, string province, string city,
        string postalCode, string addressLine)
    {
        if (string.IsNullOrWhiteSpace(recipientName))
            throw new DomainException("Recipient name is required.");

        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new DomainException("Phone number is required.");

        if (string.IsNullOrWhiteSpace(province))
            throw new DomainException("Province is required.");

        if (string.IsNullOrWhiteSpace(city))
            throw new DomainException("City is required.");

        if (string.IsNullOrWhiteSpace(postalCode))
            throw new DomainException("Postal code is required.");

        if (string.IsNullOrWhiteSpace(addressLine))
            throw new DomainException("Address is required.");

        var order = new Order(userId);

        order.ShippingRecipientName = recipientName;
        order.ShippingPhoneNumber = phoneNumber;
        order.ShippingProvince = province;
        order.ShippingCity = city;
        order.ShippingPostalCode = postalCode;
        order.ShippingAddressLine = addressLine;

        return order;
    }

    internal OrderGroup AddGroup(Guid sellerId)
    {
        var existingGroup = _groups.FirstOrDefault(x => x.SellerId == sellerId);
        if (existingGroup is not null)
            return existingGroup;

        var group = new OrderGroup(sellerId);
        _groups.Add(group);
        return group;
    }

    public void AddItem(
        Guid sellerId,
        Guid productId,
        Guid productVariantId,
        Guid sellerOfferId,
        string productName,
        string variantName,
        string sellerName,
        decimal unitPrice,
        int quantity,
        decimal discountAmount)
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException(
                "Items can only be added to a pending order.");

        var group = AddGroup(sellerId);

        group.AddItem(new OrderItem(
            productId,
            productVariantId,
            sellerOfferId,
            productName,
            variantName,
            sellerName,
            unitPrice,
            quantity,
            discountAmount));

        RecalculateTotal();
    }

    internal void RecalculateTotal()
    {
        TotalAmount = _groups.Sum(x => x.TotalAmount);
    }

    public void MoveToAwaitingPayment()
    {
        if (Status != OrderStatus.Pending)
            throw new DomainException("Order cannot move to awaiting payment.");

        Status = OrderStatus.AwaitingPayment;
    }

    public void Confirm()
    {
        if (Status != OrderStatus.AwaitingPayment)
            throw new DomainException("Only orders awaiting payment can be confirmed.");

        Status = OrderStatus.Confirmed;
    }

    public void Cancel()
    {
        if (Status != OrderStatus.Pending && Status != OrderStatus.AwaitingPayment)
            throw new DomainException("Order cannot be cancelled in its current state.");

        Status = OrderStatus.Cancelled;
    }

    internal void AddPayment(Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        _payments.Add(payment);
    }
}