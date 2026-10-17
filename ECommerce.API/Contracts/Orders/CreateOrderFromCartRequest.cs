namespace ECommerce.API.Contracts.Orders;

public sealed record CreateOrderFromCartRequest(
    string ShippingRecipientName,
    string ShippingPhoneNumber,
    string ShippingProvince,
    string ShippingCity,
    string ShippingPostalCode,
    string ShippingAddressLine);
