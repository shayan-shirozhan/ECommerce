using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Cart.UpdateCartItem;

public sealed class UpdateCartItemCommandHandler(
    ICurrentUser currentUser,
    ICartRepository cartRepository,
    ISellerOfferRepository sellerOfferRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateCartItemCommand, UpdateCartItemResponse>
{
    public async Task<UpdateCartItemResponse> Handle(
        UpdateCartItemCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
                     ?? throw new UnauthorizedException("Authenticated user is required.");

        var cart = await cartRepository.GetByUserIdAsync(userId, cancellationToken);
        if (cart is null)
        {
            throw new NotFoundException("Cart for the current user was not found.");
        }

        if (!cart.Items.Any(item => item.SellerOfferId == command.SellerOfferId))
        {
            throw new NotFoundException(
                $"Cart item for seller offer '{command.SellerOfferId}' was not found.");
        }

        var sellerOffer = await sellerOfferRepository.GetByIdAsync(
            command.SellerOfferId,
            cancellationToken);

        if (sellerOffer is null ||
            sellerOffer.Status != SellerOfferStatus.Active ||
            sellerOffer.ProductVariant is null ||
            sellerOffer.ProductVariant.Status != ProductVariantStatus.Active ||
            sellerOffer.ProductVariant.Product is null ||
            sellerOffer.ProductVariant.Product.Status != ProductStatus.Active)
        {
            throw new NotFoundException(
                $"Active seller offer with id '{command.SellerOfferId}' was not found.");
        }

        if (command.Quantity > sellerOffer.Stock)
        {
            throw new BusinessRuleException("Requested quantity exceeds available stock.");
        }

        cart.UpdateItem(command.SellerOfferId, command.Quantity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var updatedItem = cart.Items.Single(item => item.SellerOfferId == command.SellerOfferId);
        return new UpdateCartItemResponse(updatedItem.SellerOfferId, updatedItem.Quantity);
    }
}
