using FluentValidation;

namespace ECommerce.Application.Features.Catalog.ProductVariants.DeactivateProductVariant;

public sealed class DeactivateProductVariantCommandValidator : AbstractValidator<DeactivateProductVariantCommand>
{
    public DeactivateProductVariantCommandValidator()
    {
        RuleFor(x => x.ProductVariantId).NotEmpty();
    }
}
