using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Wishlist.GetMyWishlist;

public sealed class GetMyWishlistQueryHandler(
    ICurrentUser currentUser,
    IWishlistRepository wishlistRepository)
    : IRequestHandler<GetMyWishlistQuery, GetMyWishlistResponse>
{
    public async Task<GetMyWishlistResponse> Handle(
        GetMyWishlistQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Authenticated user is required.");

        var wishlist = await wishlistRepository.GetByUserIdAsync(
            userId,
            cancellationToken);

        if (wishlist is null)
            return new GetMyWishlistResponse(null, Array.Empty<WishlistItemResponse>());

        var items = wishlist.Items
            .OrderBy(item => item.Product.Name)
            .Select(item =>
            {
                var product = item.Product;
                var lowestActiveOfferPrice = product.Status == ProductStatus.Active
                    ? product.Variants
                        .Where(variant => variant.Status == ProductVariantStatus.Active)
                        .SelectMany(variant => variant.SellerOffers)
                        .Where(offer => offer.Status == SellerOfferStatus.Active)
                        .Select(offer => (decimal?)offer.Price)
                        .Min()
                    : null;

                var activeVariantIds = product.Variants
                    .Where(variant => variant.Status == ProductVariantStatus.Active)
                    .Select(variant => variant.Id)
                    .ToHashSet();

                var primaryImageUrl = product.Images
                    .Where(image => image.ProductVariantId is null
                                    || activeVariantIds.Contains(image.ProductVariantId.Value))
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.SortOrder)
                    .Select(image => image.Url)
                    .FirstOrDefault();

                return new WishlistItemResponse(
                    item.Id,
                    product.Id,
                    product.Name,
                    product.Description,
                    product.Status,
                    product.Brand?.Name,
                    product.Category?.Name,
                    primaryImageUrl,
                    lowestActiveOfferPrice);
            })
            .ToList();

        return new GetMyWishlistResponse(wishlist.Id, items);
    }
}
