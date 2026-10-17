using MediatR;

namespace ECommerce.Application.Features.Cart.AddCartItem;

public sealed record AddCartItemCommand(Guid SellerOfferId, int Quantity)
    : IRequest<AddCartItemResponse>;
