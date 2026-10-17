using FluentValidation;

namespace ECommerce.Application.Features.Cart.RemoveCartItem;

public sealed class RemoveCartItemCommandValidator : AbstractValidator<RemoveCartItemCommand>
{
    public RemoveCartItemCommandValidator()
    {
        RuleFor(command => command.SellerOfferId)
            .NotEmpty();
    }
}
