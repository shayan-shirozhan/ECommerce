using ECommerce.Domain.Enums;

namespace ECommerce.Application.Features.Orders.GetAdminOrderById;

public sealed record GetAdminOrderByIdResponse(
    Guid OrderId,
    OrderStatus Status,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    decimal TotalAmount,
    AdminOrderCustomerResponse Customer,
    AdminOrderShippingAddressResponse ShippingAddress,
    IReadOnlyList<AdminOrderGroupResponse> Groups,
    IReadOnlyList<AdminOrderPaymentResponse> Payments);

public sealed record AdminOrderCustomerResponse(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName);

public sealed record AdminOrderShippingAddressResponse(
    string RecipientName,
    string PhoneNumber,
    string Province,
    string City,
    string PostalCode,
    string AddressLine);

public sealed record AdminOrderGroupResponse(
    Guid OrderGroupId,
    Guid SellerId,
    string SellerName,
    OrderGroupStatus Status,
    decimal TotalAmount,
    string? ShippingCarrier,
    string? TrackingCode,
    IReadOnlyList<AdminOrderItemResponse> Items);

public sealed record AdminOrderItemResponse(
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

public sealed record AdminOrderPaymentResponse(
    Guid PaymentId,
    PaymentMethod PaymentMethod,
    PaymentStatus Status,
    decimal Amount,
    long? RefId,
    DateTime CreatedAt,
    DateTime? PaidAt,
    DateTime? FailedAt,
    int VerificationAttemptCount,
    DateTime? FirstVerificationAttemptedAt,
    DateTime? LastVerificationAttemptedAt,
    string? LastVerificationErrorCode,
    DateTime? ManualReviewRequiredAt);
