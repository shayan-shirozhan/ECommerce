using FluentValidation;

namespace ECommerce.Application.Features.Catalog.ProductImages.CreateProductImage;

public sealed class CreateProductImageCommandValidator : AbstractValidator<CreateProductImageCommand>
{
    public CreateProductImageCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.ProductVariantId)
            .Must(productVariantId => !productVariantId.HasValue || productVariantId.Value != Guid.Empty)
            .WithMessage("Product variant ID must not be empty when provided.");
        RuleFor(x => x.Url).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.AltText).MaximumLength(250);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}