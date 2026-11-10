using ECommerce.Application.Abstractions.Persistence;
using MediatR;

namespace ECommerce.Application.Features.Sellers.SellerRequests.GetSellerRequests;

public sealed class GetSellerRequestsQueryHandler(ISellerRequestRepository sellerRequestRepository)
    : IRequestHandler<GetSellerRequestsQuery, GetSellerRequestsResponse>
{
    public async Task<GetSellerRequestsResponse> Handle(GetSellerRequestsQuery query,
        CancellationToken cancellationToken)
    {
        var totalCount = await sellerRequestRepository.CountAsync(
            query.Status,
            cancellationToken);

        var requests = await sellerRequestRepository.GetPageAsync(
            query.Page,
            query.PageSize,
            query.Status,
            cancellationToken);

        var items = requests
            .Select(request => new SellerRequestListItemResponse(
                request.Id,
                request.UserId,
                request.Status,
                request.Reason,
                request.ReviewedByUserId,
                request.ReviewedAt))
            .ToList();

        return new GetSellerRequestsResponse(
            items,
            query.Page,
            query.PageSize,
            totalCount);
    }
}
