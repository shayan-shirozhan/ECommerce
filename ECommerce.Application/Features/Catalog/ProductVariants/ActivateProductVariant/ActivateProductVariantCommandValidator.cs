using FluentValidation;

namespace ECommerce.Application.Features.Catalog.ProductVariants.ActivateProductVariant;

public sealed class ActivateProductVariantCommandValidator : AbstractValidator<ActivateProductVariantCommand>
{
    public ActivateProductVariantCommandValidator()
    {
        RuleFor(x => x.ProductVariantId).NotEmpty();
    }
}
