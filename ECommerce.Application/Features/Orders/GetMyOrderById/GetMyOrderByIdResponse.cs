using ECommerce.Domain.Enums;

namespace ECommerce.Application.Features.Orders.GetMyOrderById;

public sealed record GetMyOrderByIdResponse(
    Guid OrderId,
    OrderStatus Status,
    DateTime CreatedAt,
    decimal TotalAmount,
    string ShippingRecipientName,
    string ShippingPhoneNumber,
    string ShippingProvince,
    string ShippingCity,
    string ShippingPostalCode,
    string ShippingAddressLine,
    IReadOnlyList<OrderGroupDetailsResponse> Groups);

public sealed record OrderGroupDetailsResponse(
    Guid OrderGroupId,
    Guid SellerId,
    OrderGroupStatus Status,
    decimal TotalAmount,
    IReadOnlyList<OrderItemDetailsResponse>  Items);

public sealed record OrderItemDetailsResponse(
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
