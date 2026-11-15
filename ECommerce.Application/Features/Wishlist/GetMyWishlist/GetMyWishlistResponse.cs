using ECommerce.Domain.Enums;

namespace ECommerce.Application.Features.Wishlist.GetMyWishlist;

public sealed record GetMyWishlistResponse(
    Guid? WishlistId,
    IReadOnlyList<WishlistItemResponse> Items);

public sealed record WishlistItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    string ProductDescription,
    ProductStatus ProductStatus,
    string? BrandName,
    string? CategoryName,
    string? PrimaryImageUrl,
    decimal? LowestActiveOfferPrice);
