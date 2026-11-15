using FluentValidation;

namespace ECommerce.Application.Features.Wishlist.AddWishlistItem;

public sealed class AddWishlistItemCommandValidator
    : AbstractValidator<AddWishlistItemCommand>
{
    public AddWishlistItemCommandValidator()
    {
        RuleFor(command => command.ProductId)
            .NotEmpty();
    }
}
