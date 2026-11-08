using ECommerce.Domain.Enums;

namespace ECommerce.Application.Features.Orders.GetSellerOrderById;

public sealed record GetSellerOrderByIdResponse(
    Guid OrderId,
    OrderStatus OrderStatus,
    DateTime CreatedAt,
    string ShippingRecipientName,
    string ShippingPhoneNumber,
    string ShippingProvince,
    string ShippingCity,
    string ShippingPostalCode,
    string ShippingAddressLine,
    Guid OrderGroupId,
    OrderGroupStatus OrderGroupStatus,
    decimal TotalAmount,
    IReadOnlyList<SellerOrderItemResponse> Items);

public sealed record SellerOrderItemResponse(
    Guid OrderItemId,
    Guid ProductId,
    Guid ProductVariantId,
    Guid SellerOfferId,
    string ProductName,
    string VariantName,
    string SellerName,
    decimal UnitPrice,
    int Quantity,
    decimal DiscountAmount,
    decimal TotalAmount);
