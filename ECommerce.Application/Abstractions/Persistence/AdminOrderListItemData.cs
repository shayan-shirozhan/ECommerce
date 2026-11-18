using ECommerce.Domain.Enums;

namespace ECommerce.Application.Abstractions.Persistence;

public sealed record AdminOrderListItemData(
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
