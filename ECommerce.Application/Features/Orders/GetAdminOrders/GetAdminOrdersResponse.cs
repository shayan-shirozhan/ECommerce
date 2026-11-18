using ECommerce.Domain.Enums;

namespace ECommerce.Application.Features.Orders.GetAdminOrders;

public sealed record GetAdminOrdersResponse(
    IReadOnlyList<AdminOrderListItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record AdminOrderListItemResponse(
    Guid OrderId,
    Guid UserId,
    string? CustomerEmail,
    string CustomerFirstName,
    string CustomerLastName,
    OrderStatus Status,
    DateTime CreatedAt,
    decimal TotalAmount,
    int OrderGroupCount,
    int ItemCount,
    bool HasPaymentRequiringManualReview);
