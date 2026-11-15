using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using UserWishlist = ECommerce.Domain.Entities.Wishlist;
using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Wishlist.AddWishlistItem;

public sealed class AddWishlistItemCommandHandler(
    ICurrentUser currentUser,
    IWishlistRepository wishlistRepository,
    IProductRepository productRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<AddWishlistItemCommand, AddWishlistItemResponse>
{
    public async Task<AddWishlistItemResponse> Handle(
        AddWishlistItemCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Authenticated user is required.");

        var product = await productRepository.GetByIdWithDetailsAsync(
            command.ProductId,
            cancellationToken);

        if (product is null || product.Status != ProductStatus.Active)
        {
            throw new NotFoundException(
                $"Active product with id '{command.ProductId}' was not found.");
        }

        var wishlist = await wishlistRepository.GetByUserIdAsync(
            userId,
            cancellationToken);

        var isNewWishlist = wishlist is null;
        if (wishlist is null)
        {
            wishlist = UserWishlist.Create(userId);
            await wishlistRepository.AddAsync(wishlist, cancellationToken);
        }

        var added = wishlist.AddProduct(product.Id);
        if (isNewWishlist || added)
            await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AddWishlistItemResponse(wishlist.Id, product.Id, added);
    }
}
