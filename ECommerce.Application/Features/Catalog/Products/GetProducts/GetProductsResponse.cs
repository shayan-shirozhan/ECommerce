namespace ECommerce.Application.Features.Catalog.Products.GetProducts;

public sealed record GetProductsResponse(
    IReadOnlyList<ProductListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
