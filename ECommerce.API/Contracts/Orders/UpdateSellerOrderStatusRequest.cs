using ECommerce.Domain.Enums;

namespace ECommerce.API.Contracts.Orders;

public sealed record UpdateSellerOrderStatusRequest(
    OrderGroupStatus NewStatus);
