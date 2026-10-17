using ECommerce.Domain.Enums;

namespace ECommerce.Application.Features.Orders.CreateOrderFromCart;

public sealed record CreateOrderFromCartResponse(
    Guid OrderId,
    OrderStatus Status,
    decimal TotalAmount,
    int GroupCount,
    int ItemCount);
