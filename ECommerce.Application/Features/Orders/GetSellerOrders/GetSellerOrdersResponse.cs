using ECommerce.Domain.Enums;

namespace ECommerce.Application.Features.Orders.GetSellerOrders;

public sealed record GetSellerOrdersResponse(
    IReadOnlyList<SellerOrderSummaryResponse> Orders);

public sealed record SellerOrderSummaryResponse(
    Guid OrderId,
    OrderStatus OrderStatus,
    Guid OrderGroupId,
    OrderGroupStatus OrderGroupStatus,
    DateTime CreatedAt,
    decimal TotalAmount,
    int ItemCount);
