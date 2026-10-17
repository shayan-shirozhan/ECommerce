using MediatR;

namespace ECommerce.Application.Features.Cart.RemoveCartItem;

public sealed record RemoveCartItemCommand(Guid SellerOfferId) : IRequest<Unit>;
