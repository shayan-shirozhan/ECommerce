using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Orders.GetSellerOrders;

public sealed class GetSellerOrdersQueryHandler(IOrderRepository orderRepository, ICurrentUser currentUser)
    : IRequestHandler<GetSellerOrdersQuery, GetSellerOrdersResponse>
{
    public async Task<GetSellerOrdersResponse> Handle(GetSellerOrdersQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedException("Authenticated user is required.");

        var orders = await orderRepository.GetBySellerIdAsync(userId, cancellationToken);

        var orderSummaries = orders.SelectMany(
            order => order.Groups.Select(
                group => new SellerOrderSummaryResponse(
                    order.Id,
                    order.Status,
                    group.Id,
                    group.Status,
                    order.CreatedAt,
                    group.TotalAmount,
                    group.Items.Count
                )
            )
        ).ToList();

        return new GetSellerOrdersResponse(orderSummaries);
    }
}
