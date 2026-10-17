using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Entities;
using MediatR;

namespace ECommerce.Application.Features.Orders.CreateOrderFromCart;

public sealed class CreateOrderFromCartCommandHandler(
    ICurrentUser currentUser,
    ICartRepository cartRepository,
    ISellerOfferRepository sellerOfferRepository,
    IOrderRepository orderRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateOrderFromCartCommand, CreateOrderFromCartResponse>
{
    public async Task<CreateOrderFromCartResponse> Handle(
        CreateOrderFromCartCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
                     ?? throw new UnauthorizedException("Authenticated user is required.");

        var cart = await cartRepository.GetByUserIdAsync(userId, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
            throw new BusinessRuleException("Cannot create an order from an empty cart.");

        var order = Order.Create(
            userId,
            command.ShippingRecipientName,
            command.ShippingPhoneNumber,
            command.ShippingProvince,
            command.ShippingCity,
            command.ShippingPostalCode,
            command.ShippingAddressLine);

        foreach (var cartItem in cart.Items)
        {
            var sellerOffer = await sellerOfferRepository.GetByIdAsync(
                cartItem.SellerOfferId,
                cancellationToken);

            if (sellerOffer is null ||
                sellerOffer.Status != SellerOfferStatus.Active ||
                sellerOffer.ProductVariant is null ||
                sellerOffer.ProductVariant.Status != ProductVariantStatus.Active ||
                sellerOffer.ProductVariant.Product is null ||
                sellerOffer.ProductVariant.Product.Status != ProductStatus.Active)
            {
                throw new NotFoundException(
                    $"Active seller offer with id '{cartItem.SellerOfferId}' was not found.");
            }

            if (cartItem.Quantity > sellerOffer.Stock)
                throw new BusinessRuleException("Requested quantity exceeds available stock.");

            var variant = sellerOffer.ProductVariant;
            var product = variant.Product;
            var productName = product.Name;
            var variantName = variant.Name;
            if (string.IsNullOrWhiteSpace(productName) || string.IsNullOrWhiteSpace(variantName))
                throw new NotFoundException("Product or variant details are unavailable.");

            var sellerName = await orderRepository.GetSellerDisplayNameAsync(
                sellerOffer.SellerId,
                cancellationToken);
            if (string.IsNullOrWhiteSpace(sellerName))
                throw new NotFoundException($"Seller '{sellerOffer.SellerId}' was not found.");

            // Keep the version read with the offer as the expected version for the update.
            // If stock changes concurrently, SaveChangesAsync will fail atomically.
            var offerForUpdate = await sellerOfferRepository.GetByIdForUpdateAsync(
                sellerOffer.Id,
                sellerOffer.RowVersion,
                cancellationToken);

            if (offerForUpdate is null || offerForUpdate.Status != SellerOfferStatus.Active)
                throw new NotFoundException(
                    $"Active seller offer with id '{cartItem.SellerOfferId}' was not found.");

            if (cartItem.Quantity > offerForUpdate.Stock)
                throw new BusinessRuleException("Requested quantity exceeds available stock.");

            offerForUpdate.UpdateStock(offerForUpdate.Stock - cartItem.Quantity);

            order.AddItem(
                sellerOffer.SellerId,
                product.Id,
                variant.Id,
                sellerOffer.Id,
                productName,
                variantName,
                sellerName,
                sellerOffer.Price,
                cartItem.Quantity,
                discountAmount: 0m);
        }

        await orderRepository.AddAsync(order, cancellationToken);

        foreach (var cartItem in cart.Items.ToArray())
            cart.RemoveItem(cartItem.SellerOfferId);

        // A single relational SaveChanges persists the order, stock changes, and cart deletes
        // in one database transaction. On a failure, none of those changes should be committed.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateOrderFromCartResponse(
            order.Id,
            order.Status,
            order.TotalAmount,
            order.Groups.Count,
            order.Groups.Sum(group => group.Items.Count));
    }
}
