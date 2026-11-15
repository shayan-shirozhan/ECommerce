using FluentValidation;

namespace ECommerce.Application.Features.Catalog.ProductVariants.UpdateProductVariant;

public sealed class UpdateProductVariantCommandValidator
    : AbstractValidator<UpdateProductVariantCommand>
{
    public UpdateProductVariantCommandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();

        RuleFor(command => command.SKU)
            .NotEmpty()
            .MaximumLength(100);

        When(command => command.Name is not null, () =>
        {
            RuleFor(command => command.Name)
                .NotEmpty()
                .MaximumLength(150);
        });
    }
}
