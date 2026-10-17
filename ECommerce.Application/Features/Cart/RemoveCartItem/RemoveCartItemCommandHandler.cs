using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Cart.RemoveCartItem;

public sealed class RemoveCartItemCommandHandler(
    ICurrentUser currentUser,
    ICartRepository cartRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RemoveCartItemCommand, Unit>
{
    public async Task<Unit> Handle(
        RemoveCartItemCommand command,
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

        cart.RemoveItem(command.SellerOfferId);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
