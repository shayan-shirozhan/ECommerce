using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

internal sealed class ProductRepository(ECommerceDbContext dbContext) : IProductRepository
{
    public async Task<Product?> GetByIdWithDetailsAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Products.AsNoTracking().AsSplitQuery()
            .Include(x => x.Brand)
            .Include(x => x.Category)
            .Include(x => x.Variants)
            .ThenInclude(x => x.SellerOffers)
            .Include(x => x.Images)
            .FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);
    }

    public async Task<Product?> GetByIdForUpdateAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Products.FirstOrDefaultAsync(x => x.Id == productId, cancellationToken);
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        await dbContext.Products.AddAsync(product, cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Products.AnyAsync(x => x.Id == productId, cancellationToken);
    }
}
