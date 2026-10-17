using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Cart.GetCart;

public sealed class GetCartQueryHandler(
    ICurrentUser currentUser,
    ICartRepository cartRepository,
    ISellerOfferRepository sellerOfferRepository)
    : IRequestHandler<GetCartQuery, GetCartResponse>
{
    public async Task<GetCartResponse> Handle(
        GetCartQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Authenticated user is required.");

        var cart = await cartRepository.GetByUserIdAsync(userId, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
        {
            return new GetCartResponse(Array.Empty<GetCartItemResponse>(), 0m);
        }

        var responseItems = new List<GetCartItemResponse>(cart.Items.Count);

        foreach (var cartItem in cart.Items)
        {
            var sellerOffer = await sellerOfferRepository.GetByIdAsync(
                cartItem.SellerOfferId,
                cancellationToken);

            var variant = sellerOffer?.ProductVariant;
            var product = variant?.Product;
            if (sellerOffer is null || variant is null || product is null)
            {
                throw new NotFoundException(
                    $"Seller offer with id '{cartItem.SellerOfferId}' referenced by the cart was not found.");
            }

            var lineTotal = sellerOffer.Price * cartItem.Quantity;
            responseItems.Add(new GetCartItemResponse(
                sellerOffer.Id,
                product.Name,
                variant.Name,
                cartItem.Quantity,
                sellerOffer.Price,
                lineTotal));
        }

        return new GetCartResponse(
            responseItems,
            responseItems.Sum(item => item.LineTotal));
    }
}
