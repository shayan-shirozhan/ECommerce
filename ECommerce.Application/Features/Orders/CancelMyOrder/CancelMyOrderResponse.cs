using ECommerce.Domain.Enums;

namespace ECommerce.Application.Features.Orders.CancelMyOrder;

public sealed record CancelMyOrderResponse(
    Guid OrderId,
    OrderStatus OrderStatus,
    int RestoredItemCount,
    long RestoredQuantity);
