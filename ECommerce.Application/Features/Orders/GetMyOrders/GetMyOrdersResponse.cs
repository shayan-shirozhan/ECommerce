using ECommerce.Domain.Enums;

namespace ECommerce.Application.Features.Orders.GetMyOrders;

public sealed record GetMyOrdersResponse(
    IReadOnlyList<OrderSummaryResponse> Orders);

public sealed record OrderSummaryResponse(
    Guid OrderId,
    OrderStatus Status,
    DateTime CreatedAt,
    int OrderItemCount,
    decimal TotalAmount);
