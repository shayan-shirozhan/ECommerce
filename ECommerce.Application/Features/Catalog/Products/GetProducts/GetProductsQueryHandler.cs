using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Features.Catalog.Brands.GetBrandById;
using ECommerce.Application.Features.Catalog.Categories.GetCategoryById;
using MediatR;

namespace ECommerce.Application.Features.Catalog.Products.GetProducts;

public sealed class GetProductsQueryHandler(
    IProductRepository productRepository)
    : IRequestHandler<GetProductsQuery, GetProductsResponse>
{
    public async Task<GetProductsResponse> Handle(
        GetProductsQuery query,
        CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(query.Search)
            ? null
            : query.Search.Trim();

        var filter = new ProductListFilter(
            query.CategoryId,
            query.BrandId,
            query.MinPrice,
            query.MaxPrice,
            search,
            ParseSortOrder(query.SortBy));

        var totalCount = await productRepository.CountActiveAsync(
            filter,
            cancellationToken);

        var products = await productRepository.GetActivePageAsync(
            query.Page,
            query.PageSize,
            filter,
            cancellationToken);

        var items = products
            .Select(product => new ProductListItemDto(
                product.Id,
                product.Name,
                product.Description,
                new BrandDto(product.BrandId, product.BrandName),
                new CategoryDto(product.CategoryId, product.CategoryName),
                product.CreatedAt,
                product.Price))
            .ToList();

        return new GetProductsResponse(
            items,
            query.Page,
            query.PageSize,
            totalCount);
    }

    private static ProductSortOrder ParseSortOrder(string? sortBy)
    {
        return sortBy?.Trim().ToLowerInvariant() switch
        {
            ProductSortOptions.PriceAscending =>
                ProductSortOrder.PriceAscending,

            ProductSortOptions.PriceDescending =>
                ProductSortOrder.PriceDescending,

            _ => ProductSortOrder.Newest
        };
    }
}
