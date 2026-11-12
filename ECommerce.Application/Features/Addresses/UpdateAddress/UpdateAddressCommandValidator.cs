using FluentValidation;

namespace ECommerce.Application.Features.Addresses.UpdateAddress;

public sealed class UpdateAddressCommandValidator : AbstractValidator<UpdateAddressCommand>
{
    public UpdateAddressCommandValidator()
    {
        RuleFor(command => command.AddressId)
            .NotEmpty();

        RuleFor(command => command.Title)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.RecipientName)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(command => command.PhoneNumber)
            .NotEmpty()
            .MaximumLength(30);

        RuleFor(command => command.Province)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.City)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.PostalCode)
            .NotEmpty()
            .MaximumLength(30);

        RuleFor(command => command.AddressLine)
            .NotEmpty()
            .MaximumLength(500);
    }
}
