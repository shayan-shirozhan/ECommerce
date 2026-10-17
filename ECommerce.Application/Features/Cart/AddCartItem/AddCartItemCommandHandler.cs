using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Domain.Enums;
using MediatR;
using ShoppingCart = ECommerce.Domain.Entities.Cart;

namespace ECommerce.Application.Features.Cart.AddCartItem;

public sealed class AddCartItemCommandHandler(
    ICurrentUser currentUser,
    ICartRepository cartRepository,
    ISellerOfferRepository sellerOfferRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<AddCartItemCommand, AddCartItemResponse>
{
    public async Task<AddCartItemResponse> Handle(
        AddCartItemCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
                     ?? throw new UnauthorizedException("Authenticated user is required.");

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

        var cart = await cartRepository.GetByUserIdAsync(userId, cancellationToken);
        var existingItem = cart?.Items.FirstOrDefault(item => item.SellerOfferId == sellerOffer.Id);
        var currentQuantity = existingItem?.Quantity ?? 0;

        // Subtract before comparing so the requested quantity cannot overflow an int sum.
        if (command.Quantity > sellerOffer.Stock - currentQuantity)
        {
            throw new BusinessRuleException("Requested quantity exceeds available stock.");
        }

        if (cart is null)
        {
            cart = ShoppingCart.Create(userId);
            await cartRepository.AddAsync(cart, cancellationToken);
        }

        cart.AddItem(sellerOffer.Id, command.Quantity);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var updatedItem = cart.Items.Single(item => item.SellerOfferId == sellerOffer.Id);
        return new AddCartItemResponse(updatedItem.SellerOfferId, updatedItem.Quantity);
    }
}
