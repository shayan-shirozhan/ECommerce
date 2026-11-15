using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Wishlist.RemoveWishlistItem;

public sealed class RemoveWishlistItemCommandHandler(
    ICurrentUser currentUser,
    IWishlistRepository wishlistRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RemoveWishlistItemCommand, Unit>
{
    public async Task<Unit> Handle(
        RemoveWishlistItemCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Authenticated user is required.");

        var wishlist = await wishlistRepository.GetByUserIdAsync(
            userId,
            cancellationToken);

        if (wishlist is not null && wishlist.RemoveProduct(command.ProductId))
            await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
