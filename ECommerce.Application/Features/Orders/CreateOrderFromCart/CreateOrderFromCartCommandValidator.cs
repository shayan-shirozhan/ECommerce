using FluentValidation;

namespace ECommerce.Application.Features.Orders.CreateOrderFromCart;

public sealed class CreateOrderFromCartCommandValidator : AbstractValidator<CreateOrderFromCartCommand>
{
    public CreateOrderFromCartCommandValidator()
    {
        RuleFor(command => command.ShippingRecipientName)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(command => command.ShippingPhoneNumber)
            .NotEmpty()
            .MaximumLength(30);

        RuleFor(command => command.ShippingProvince)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.ShippingCity)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.ShippingPostalCode)
            .NotEmpty()
            .MaximumLength(30);

        RuleFor(command => command.ShippingAddressLine)
            .NotEmpty()
            .MaximumLength(500);
    }
}
