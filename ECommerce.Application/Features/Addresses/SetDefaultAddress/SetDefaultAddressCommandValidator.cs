using FluentValidation;

namespace ECommerce.Application.Features.Addresses.SetDefaultAddress;

public sealed class SetDefaultAddressCommandValidator : AbstractValidator<SetDefaultAddressCommand>
{
    public SetDefaultAddressCommandValidator()
    {
        RuleFor(command => command.AddressId)
            .NotEmpty();
    }
}
