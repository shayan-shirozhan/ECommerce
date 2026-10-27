using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Orders.GetMyOrders;

public sealed class GetMyOrdersQueryHandler(
    ICurrentUser currentUser,
    IOrderRepository orderRepository)
    : IRequestHandler<GetMyOrdersQuery, GetMyOrdersResponse>
{
    public async Task<GetMyOrdersResponse> Handle(
        GetMyOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
                     ?? throw new UnauthorizedException(
                         "Authenticated user is required.");

        var orders = await orderRepository.GetByUserIdAsync(
            userId,
            cancellationToken);

        var orderSummaries = orders
            .Select(order => new OrderSummaryResponse(
                order.Id,
                order.Status,
                order.CreatedAt,
                order.Groups.Sum(group => group.Items.Count),
                order.TotalAmount))
            .ToList();

        return new GetMyOrdersResponse(orderSummaries);
    }
}
