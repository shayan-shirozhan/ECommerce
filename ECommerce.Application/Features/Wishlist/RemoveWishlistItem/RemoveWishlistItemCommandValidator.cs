using FluentValidation;

namespace ECommerce.Application.Features.Wishlist.RemoveWishlistItem;

public sealed class RemoveWishlistItemCommandValidator
    : AbstractValidator<RemoveWishlistItemCommand>
{
    public RemoveWishlistItemCommandValidator()
    {
        RuleFor(command => command.ProductId)
            .NotEmpty();
    }
}
