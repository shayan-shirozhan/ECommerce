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
        var totalCount = await productRepository.CountActiveAsync(
            cancellationToken);

        var products = await productRepository.GetActivePageAsync(
            query.Page,
            query.PageSize,
            cancellationToken);

        var items = products
            .Select(product => new ProductListItemDto(
                product.Id,
                product.Name,
                product.Description,
                new BrandDto(
                    product.Brand!.Id,
                    product.Brand.Name),
                new CategoryDto(
                    product.Category.Id,
                    product.Category.Name),
                product.CreatedAt))
            .ToList();

        return new GetProductsResponse(
            items,
            query.Page,
            query.PageSize,
            totalCount);
    }
}
