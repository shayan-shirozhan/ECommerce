using FluentValidation;

namespace ECommerce.Application.Features.Catalog.Products.DeactivateProduct;

public sealed class DeactivateProductCommandValidator : AbstractValidator<DeactivateProductCommand>
{
    public DeactivateProductCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}
