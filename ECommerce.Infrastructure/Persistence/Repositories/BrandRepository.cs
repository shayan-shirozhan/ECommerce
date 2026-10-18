using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

internal sealed class BrandRepository(
    ECommerceDbContext dbContext) : IBrandRepository
{
    public async Task<bool> ExistsAsync(
        Guid brandId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Brands.AnyAsync(
            brand => brand.Id == brandId && !brand.IsDeleted,
            cancellationToken);
    }

    public async Task AddAsync(
        Brand brand,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Brands.AddAsync(brand, cancellationToken);
    }

    public async Task<Brand?> GetByIdAsync(
        Guid brandId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Brands
            .AsNoTracking()
            .FirstOrDefaultAsync(
                brand => brand.Id == brandId && !brand.IsDeleted,
                cancellationToken);
    }

    public async Task<Brand?> GetByIdForUpdateAsync(
        Guid brandId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Brands.FirstOrDefaultAsync(
            brand => brand.Id == brandId && !brand.IsDeleted,
            cancellationToken);
    }

    public async Task<bool> HasActiveProductsAsync(
        Guid brandId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Products.AnyAsync(
            product =>
                product.BrandId == brandId
                && product.Status == ProductStatus.Active,
            cancellationToken);
    }
}
