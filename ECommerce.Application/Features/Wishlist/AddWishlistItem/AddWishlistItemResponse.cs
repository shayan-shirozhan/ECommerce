namespace ECommerce.Application.Features.Wishlist.AddWishlistItem;

public sealed record AddWishlistItemResponse(
    Guid WishlistId,
    Guid ProductId,
    bool Added);
