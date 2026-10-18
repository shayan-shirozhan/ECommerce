using FluentValidation;

namespace ECommerce.Application.Features.Catalog.Brands.UpdateBrand;

public sealed class UpdateBrandCommandValidator
    : AbstractValidator<UpdateBrandCommand>
{
    public UpdateBrandCommandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(150);
    }
}
