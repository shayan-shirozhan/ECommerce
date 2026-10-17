using FluentValidation;

namespace ECommerce.Application.Features.Cart.UpdateCartItem;

public sealed class UpdateCartItemCommandValidator : AbstractValidator<UpdateCartItemCommand>
{
    public UpdateCartItemCommandValidator()
    {
        RuleFor(command => command.SellerOfferId)
            .NotEmpty();

        RuleFor(command => command.Quantity)
            .GreaterThan(0);
    }
}
