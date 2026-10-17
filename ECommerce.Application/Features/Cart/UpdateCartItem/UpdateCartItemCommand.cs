using MediatR;

namespace ECommerce.Application.Features.Cart.UpdateCartItem;

public sealed record UpdateCartItemCommand(Guid SellerOfferId, int Quantity)
    : IRequest<UpdateCartItemResponse>;
