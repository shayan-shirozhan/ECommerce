using FluentValidation;

namespace ECommerce.Application.Features.Catalog.Products.ActivateProduct;

public sealed class ActivateProductCommandValidator : AbstractValidator<ActivateProductCommand>
{
    public ActivateProductCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
    }
}
