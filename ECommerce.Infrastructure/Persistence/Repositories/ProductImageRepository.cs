using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

internal sealed class ProductImageRepository(ECommerceDbContext dbContext) : IProductImageRepository
{
    public async Task AddAsync(ProductImage productImage, CancellationToken cancellationToken = default)
    {
        await dbContext.ProductImages.AddAsync(productImage, cancellationToken);
    }

    public async Task<ProductImage?> GetByIdAsync(Guid productImageId, CancellationToken cancellationToken = default)
    {
        return await dbContext.ProductImages
            .AsNoTracking()
            .Include(x => x.Product)
            .Include(x => x.ProductVariant)
            .ThenInclude(x => x!.Product)
            .FirstOrDefaultAsync(x => x.Id == productImageId, cancellationToken);
    }
}
