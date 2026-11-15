using MediatR;

namespace ECommerce.Application.Features.Wishlist.RemoveWishlistItem;

public sealed record RemoveWishlistItemCommand(Guid ProductId) : IRequest<Unit>;
