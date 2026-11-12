using FluentValidation;

namespace ECommerce.Application.Features.Addresses.DeleteAddress;

public sealed class DeleteAddressCommandValidator : AbstractValidator<DeleteAddressCommand>
{
    public DeleteAddressCommandValidator()
    {
        RuleFor(command => command.AddressId)
            .NotEmpty();
    }
}
