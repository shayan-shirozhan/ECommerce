using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Orders.GetAdminOrders;

public sealed record GetAdminOrdersQuery(
    int Page = 1,
    int PageSize = 10,
    OrderStatus? Status = null,
    DateTimeOffset? CreatedFromUtc = null,
    DateTimeOffset? CreatedToUtc = null)
    : IRequest<GetAdminOrdersResponse>
{
    public const int MaxPageSize = 100;
}
