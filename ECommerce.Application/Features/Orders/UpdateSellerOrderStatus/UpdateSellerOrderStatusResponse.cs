using ECommerce.Domain.Enums;

namespace ECommerce.Application.Features.Orders.UpdateSellerOrderStatus;

public sealed record UpdateSellerOrderStatusResponse(
    Guid OrderId,
    OrderStatus OrderStatus,
    Guid OrderGroupId,
    OrderGroupStatus OrderGroupStatus);
