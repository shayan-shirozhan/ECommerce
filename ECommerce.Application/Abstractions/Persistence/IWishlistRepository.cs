using ECommerce.Domain.Entities;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IWishlistRepository
{
    Task<Wishlist?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Wishlist wishlist,
        CancellationToken cancellationToken = default);
}
