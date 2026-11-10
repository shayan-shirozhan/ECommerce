using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Sellers.SellerRequests.GetSellerRequests;

public sealed record GetSellerRequestsQuery(
    int Page = 1,
    int PageSize = 10,
    SellerRequestStatus? Status = null)
    : IRequest<GetSellerRequestsResponse>
{
    public const int MaxPageSize = 100;
}
