using ECommerce.Domain.Common;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Domain.Entities;

public class Wishlist : AuditableEntity
{
    private Wishlist()
    {
    }

    private Wishlist(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new DomainException("User ID is required.");

        UserId = userId;
    }

    public Guid UserId { get; private set; }

    private readonly List<WishlistItem> _items = [];
    public IReadOnlyCollection<WishlistItem> Items => _items.AsReadOnly();

    public static Wishlist Create(Guid userId) => new(userId);

    /// <summary>
    /// Adds a product to the wishlist. Returns false when the product is already present.
    /// </summary>
    public bool AddProduct(Guid productId)
    {
        if (productId == Guid.Empty)
            throw new DomainException("Product ID is required.");

        if (_items.Any(item => item.ProductId == productId))
            return false;

        _items.Add(new WishlistItem(productId));
        return true;
    }

    /// <summary>
    /// Removes a product from the wishlist. Returns false when it is not present.
    /// </summary>
    public bool RemoveProduct(Guid productId)
    {
        if (productId == Guid.Empty)
            throw new DomainException("Product ID is required.");

        var item = _items.FirstOrDefault(candidate => candidate.ProductId == productId);
        if (item is null)
            return false;

        _items.Remove(item);
        return true;
    }
}
