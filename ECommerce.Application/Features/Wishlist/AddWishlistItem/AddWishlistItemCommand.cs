using MediatR;

namespace ECommerce.Application.Features.Wishlist.AddWishlistItem;

public sealed record AddWishlistItemCommand(Guid ProductId)
    : IRequest<AddWishlistItemResponse>;
