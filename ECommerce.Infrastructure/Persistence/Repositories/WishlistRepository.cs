using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

internal sealed class WishlistRepository(ECommerceDbContext dbContext) : IWishlistRepository
{
    public Task<Wishlist?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Wishlists
            .AsSplitQuery()
            .Include(wishlist => wishlist.Items)
                .ThenInclude(item => item.Product)
                    .ThenInclude(product => product.Brand)
            .Include(wishlist => wishlist.Items)
                .ThenInclude(item => item.Product)
                    .ThenInclude(product => product.Category)
            .Include(wishlist => wishlist.Items)
                .ThenInclude(item => item.Product)
                    .ThenInclude(product => product.Images)
            .Include(wishlist => wishlist.Items)
                .ThenInclude(item => item.Product)
                    .ThenInclude(product => product.Variants)
                        .ThenInclude(variant => variant.SellerOffers)
            .FirstOrDefaultAsync(
                wishlist => wishlist.UserId == userId,
                cancellationToken);
    }

    public async Task AddAsync(
        Wishlist wishlist,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Wishlists.AddAsync(wishlist, cancellationToken);
    }
}
