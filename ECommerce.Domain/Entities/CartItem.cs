using ECommerce.Domain.Common;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Domain.Entities;

public class CartItem : Entity
{
    //ctor's
    private CartItem()
    {

    }

    internal CartItem(Guid sellerOfferId, int quantity)
    {
        if (sellerOfferId == Guid.Empty)
            throw new DomainException("Seller offer ID is required.");

        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero.");

        SellerOfferId = sellerOfferId;
        Quantity = quantity;
    }


    //prop's
    public Guid CartId { get; private set; }
    public Guid SellerOfferId { get; private set; }
    public int Quantity { get; private set; }


    //method's
    internal void IncreaseQuantity(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero.");

        if (quantity > int.MaxValue - Quantity)
            throw new DomainException(
                "Cart item quantity exceeds the maximum supported value.");

        Quantity += quantity;
    }

    internal void SetQuantity(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Quantity must be greater than zero.");

        Quantity = quantity;
    }
}
