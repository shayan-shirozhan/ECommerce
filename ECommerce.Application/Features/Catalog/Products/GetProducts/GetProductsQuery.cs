using MediatR;

namespace ECommerce.Application.Features.Catalog.Products.GetProducts;

public sealed record GetProductsQuery(
    int Page = 1,
    int PageSize = 10) : IRequest<GetProductsResponse>
{
    public const int MaxPageSize = 100;
}
