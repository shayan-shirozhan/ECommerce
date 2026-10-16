using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

internal sealed class SellerOfferRepository(ECommerceDbContext dbContext) : ISellerOfferRepository
{
    public async Task AddAsync(SellerOffer sellerOffer, CancellationToken cancellationToken = default)
    {
        await dbContext.SellerOffers.AddAsync(sellerOffer, cancellationToken);
    }

    public async Task<SellerOffer?> GetByIdAsync(Guid sellerOfferId, CancellationToken cancellationToken = default)
    {
        return await dbContext.SellerOffers.AsNoTracking().Include(x => x.ProductVariant)
            .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == sellerOfferId, cancellationToken);
    }

    public async Task<SellerOffer?> GetByIdForUpdateAsync(Guid sellerOfferId, uint expectedRowVersion,
        CancellationToken cancellationToken = default)
    {
        var sellerOffer =
            await dbContext.SellerOffers.FirstOrDefaultAsync(x => x.Id == sellerOfferId, cancellationToken);
        if (sellerOffer is null) return null;

        dbContext.Entry(sellerOffer).Property(x => x.RowVersion).OriginalValue = expectedRowVersion;
        return sellerOffer;
    }
}
