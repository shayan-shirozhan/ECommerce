using ECommerce.Application.Abstractions.Persistence;
using MediatR;

namespace ECommerce.Application.Features.Orders.GetAdminOrders;

public sealed class GetAdminOrdersQueryHandler(IOrderRepository orderRepository)
    : IRequestHandler<GetAdminOrdersQuery, GetAdminOrdersResponse>
{
    public async Task<GetAdminOrdersResponse> Handle(
        GetAdminOrdersQuery query,
        CancellationToken cancellationToken)
    {
        var createdFromUtc = query.CreatedFromUtc?.UtcDateTime;
        var createdToUtc = query.CreatedToUtc?.UtcDateTime;

        var totalCount = await orderRepository.CountAdminAsync(
            query.Status,
            createdFromUtc,
            createdToUtc,
            query.HasManualReview,
            cancellationToken);

        var orders = await orderRepository.GetAdminPageAsync(
            query.Page,
            query.PageSize,
            query.Status,
            createdFromUtc,
            createdToUtc,
            query.HasManualReview,
            cancellationToken);

        var items = orders
            .Select(order => new AdminOrderListItemResponse(
                order.OrderId,
                order.UserId,
                order.CustomerEmail,
                order.CustomerFirstName,
                order.CustomerLastName,
                order.Status,
                order.CreatedAt,
                order.TotalAmount,
                order.OrderGroupCount,
                order.ItemCount,
                order.HasPaymentRequiringManualReview))
            .ToList();

        return new GetAdminOrdersResponse(
            items,
            query.Page,
            query.PageSize,
            totalCount);
    }
}
