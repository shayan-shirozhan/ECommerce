using ECommerce.Domain.Common;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Domain.Entities;

public class WishlistItem : Entity
{
    private WishlistItem()
    {
    }

    internal WishlistItem(Guid productId)
    {
        if (productId == Guid.Empty)
            throw new DomainException("Product ID is required.");

        ProductId = productId;
    }

    public Guid WishlistId { get; private set; }
    public Guid ProductId { get; private set; }
    public Product Product { get; private set; } = null!;
}
