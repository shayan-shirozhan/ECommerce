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

    public async Task<IReadOnlyList<ProductListingItem>> GetActivePageAsync(
        int page,
        int pageSize,
        ProductListFilter filter,
        CancellationToken cancellationToken = default)
    {
        var offset = checked((int)(((long)page - 1L) * pageSize));

        var query = BuildActiveProductsQuery(filter);
        var orderedQuery = ApplyOrdering(query, filter.SortOrder);

        return await orderedQuery
            .Skip(offset)
            .Take(pageSize)
            .Select(product => new ProductListingItem(
                product.Id,
                product.Name,
                product.Description,
                product.BrandId,
                product.Brand!.Name,
                product.CategoryId,
                product.Category.Name,
                product.CreatedAt,
                product.Variants
                    .Where(variant =>
                        variant.Status == ProductVariantStatus.Active)
                    .SelectMany(variant => variant.SellerOffers)
                    .Where(offer =>
                        offer.Status == SellerOfferStatus.Active)
                    .Select(offer => (decimal?)offer.Price)
                    .Min()))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountActiveAsync(
        ProductListFilter filter,
        CancellationToken cancellationToken = default)
    {
        return await BuildActiveProductsQuery(filter)
            .CountAsync(cancellationToken);
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

    private IQueryable<Product> BuildActiveProductsQuery(
        ProductListFilter filter)
    {
        IQueryable<Product> query = dbContext.Products
            .AsNoTracking()
            .Where(product => product.Status == ProductStatus.Active);

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(product =>
                product.CategoryId == filter.CategoryId.Value);
        }

        if (filter.BrandId.HasValue)
        {
            query = query.Where(product =>
                product.BrandId == filter.BrandId.Value);
        }

        if (filter.MinPrice.HasValue)
        {
            var minPrice = filter.MinPrice.Value;

            query = query.Where(product =>
                product.Variants
                    .Where(variant =>
                        variant.Status == ProductVariantStatus.Active)
                    .SelectMany(variant => variant.SellerOffers)
                    .Where(offer =>
                        offer.Status == SellerOfferStatus.Active)
                    .Select(offer => (decimal?)offer.Price)
                    .Min() >= minPrice);
        }

        if (filter.MaxPrice.HasValue)
        {
            var maxPrice = filter.MaxPrice.Value;

            query = query.Where(product =>
                product.Variants
                    .Where(variant =>
                        variant.Status == ProductVariantStatus.Active)
                    .SelectMany(variant => variant.SellerOffers)
                    .Where(offer =>
                        offer.Status == SellerOfferStatus.Active)
                    .Select(offer => (decimal?)offer.Price)
                    .Min() <= maxPrice);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var escapedSearch = EscapeLikePattern(filter.Search.Trim());
            var pattern = $"%{escapedSearch}%";

            query = query.Where(product =>
                EF.Functions.ILike(product.Name, pattern, "\\")
                || EF.Functions.ILike(product.Description, pattern, "\\"));
        }

        return query;
    }

    private static IOrderedQueryable<Product> ApplyOrdering(
        IQueryable<Product> query,
        ProductSortOrder sortOrder)
    {
        return sortOrder switch
        {
            ProductSortOrder.PriceAscending => query
                .OrderBy(product =>
                    product.Variants
                        .Where(variant =>
                            variant.Status == ProductVariantStatus.Active)
                        .SelectMany(variant => variant.SellerOffers)
                        .Where(offer =>
                            offer.Status == SellerOfferStatus.Active)
                        .Select(offer => (decimal?)offer.Price)
                        .Min() == null)
                .ThenBy(product =>
                    product.Variants
                        .Where(variant =>
                            variant.Status == ProductVariantStatus.Active)
                        .SelectMany(variant => variant.SellerOffers)
                        .Where(offer =>
                            offer.Status == SellerOfferStatus.Active)
                        .Select(offer => (decimal?)offer.Price)
                        .Min())
                .ThenByDescending(product => product.CreatedAt)
                .ThenBy(product => product.Id),

            ProductSortOrder.PriceDescending => query
                .OrderBy(product =>
                    product.Variants
                        .Where(variant =>
                            variant.Status == ProductVariantStatus.Active)
                        .SelectMany(variant => variant.SellerOffers)
                        .Where(offer =>
                            offer.Status == SellerOfferStatus.Active)
                        .Select(offer => (decimal?)offer.Price)
                        .Min() == null)
                .ThenByDescending(product =>
                    product.Variants
                        .Where(variant =>
                            variant.Status == ProductVariantStatus.Active)
                        .SelectMany(variant => variant.SellerOffers)
                        .Where(offer =>
                            offer.Status == SellerOfferStatus.Active)
                        .Select(offer => (decimal?)offer.Price)
                        .Min())
                .ThenByDescending(product => product.CreatedAt)
                .ThenBy(product => product.Id),

            _ => query
                .OrderByDescending(product => product.CreatedAt)
                .ThenBy(product => product.Id)
        };
    }

    private static string EscapeLikePattern(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }
}
