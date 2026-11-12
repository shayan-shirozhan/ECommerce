using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
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


    public async Task<IReadOnlyList<SellerOffer>> GetByIdsForUpdateAsync(
        IReadOnlyCollection<Guid> sellerOfferIds,
        CancellationToken cancellationToken = default)
    {
        if (sellerOfferIds.Count == 0)
            return [];

        // Keep the entities tracked so EF Core uses their current RowVersion values
        // as concurrency tokens when stock is updated.
        return await dbContext.SellerOffers
            .Where(offer => sellerOfferIds.Contains(offer.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SellerOfferListingItem>> GetBySellerIdPageAsync(Guid sellerId, int page,
        int pageSize, SellerOfferStatus? status = null, CancellationToken cancellationToken = default)
    {
        var offset = checked((int)(((long)page - 1L) * pageSize));

        var query = dbContext.SellerOffers
            .AsNoTracking()
            .Where(offer => offer.SellerId == sellerId);

        if (status.HasValue)
            query = query.Where(offer => offer.Status == status.Value);

        return await query
            .OrderByDescending(offer => offer.CreatedAt)
            .ThenByDescending(offer => offer.Id)
            .Skip(offset)
            .Take(pageSize)
            .Select(offer => new SellerOfferListingItem(
                offer.Id,
                offer.ProductVariant.ProductId,
                offer.ProductVariant.Product.Name,
                offer.ProductVariantId,
                offer.ProductVariant.Name,
                offer.ProductVariant.SKU,
                offer.Price,
                offer.Stock,
                offer.Status,
                offer.RowVersion,
                offer.CreatedAt))
            .ToListAsync(cancellationToken);
    }


    public Task<int> CountBySellerIdAsync(Guid sellerId, SellerOfferStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.SellerOffers
            .AsNoTracking()
            .Where(offer => offer.SellerId == sellerId);

        if (status.HasValue)
            query = query.Where(offer => offer.Status == status.Value);

        return query.CountAsync(cancellationToken);
    }
}
