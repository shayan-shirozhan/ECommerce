using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

internal sealed class ProductRepository(
    ECommerceDbContext dbContext) : IProductRepository
{
    public async Task<Product?> GetByIdWithDetailsAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Products
            .AsNoTracking()
            .AsSplitQuery()
            .Include(product => product.Brand)
            .Include(product => product.Category)
            .Include(product => product.Variants)
                .ThenInclude(variant => variant.SellerOffers)
            .Include(product => product.Images)
            .FirstOrDefaultAsync(
                product => product.Id == productId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetActivePageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var offset = checked((int)(((long)page - 1L) * pageSize));

        return await dbContext.Products
            .AsNoTracking()
            .Where(product => product.Status == ProductStatus.Active)
            .Include(product => product.Brand)
            .Include(product => product.Category)
            .OrderByDescending(product => product.CreatedAt)
            .ThenBy(product => product.Id)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountActiveAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Products
            .AsNoTracking()
            .CountAsync(
                product => product.Status == ProductStatus.Active,
                cancellationToken);
    }

    public async Task<Product?> GetByIdForUpdateAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Products.FirstOrDefaultAsync(
            product => product.Id == productId,
            cancellationToken);
    }

    public async Task AddAsync(
        Product product,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Products.AddAsync(product, cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Products.AnyAsync(
            product => product.Id == productId,
            cancellationToken);
    }
}
