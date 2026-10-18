using MediatR;

namespace ECommerce.Application.Features.Catalog.Products.GetProducts;

public sealed record GetProductsQuery(
    int Page = 1,
    int PageSize = 10,
    Guid? CategoryId = null,
    Guid? BrandId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? Search = null,
    string SortBy = ProductSortOptions.Newest)
    : IRequest<GetProductsResponse>
{
    public const int MaxPageSize = 100;
}
