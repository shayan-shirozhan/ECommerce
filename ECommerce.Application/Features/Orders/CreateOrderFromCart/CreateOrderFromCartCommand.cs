using MediatR;

namespace ECommerce.Application.Features.Orders.CreateOrderFromCart;

public sealed record CreateOrderFromCartCommand(
    string ShippingRecipientName,
    string ShippingPhoneNumber,
    string ShippingProvince,
    string ShippingCity,
    string ShippingPostalCode,
    string ShippingAddressLine) : IRequest<CreateOrderFromCartResponse>;
