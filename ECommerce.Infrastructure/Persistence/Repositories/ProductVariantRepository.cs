using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

internal sealed class ProductVariantRepository(ECommerceDbContext dbContext) : IProductVariantRepository
{
    public async Task<bool> ExistsAsync(Guid productVariantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ProductVariants.AnyAsync(x => x.Id == productVariantId, cancellationToken);
    }

    public async Task AddAsync(ProductVariant productVariant, CancellationToken cancellationToken = default)
    {
        await dbContext.ProductVariants.AddAsync(productVariant, cancellationToken);
    }

    public async Task<ProductVariant?> GetByIdWithDetailsAsync(Guid productVariantId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.ProductVariants
            .AsNoTracking().Include(x => x.Product).Include(x => x.SellerOffers)
            .FirstOrDefaultAsync(x => x.Id == productVariantId, cancellationToken);
    }

    public async Task<ProductVariant?> GetByIdForUpdateAsync(Guid productVariantId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.ProductVariants
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == productVariantId, cancellationToken);
    }
}
