using FluentValidation;

namespace ECommerce.Application.Features.Catalog.ProductImages.DeleteProductImage;

public sealed class DeleteProductImageCommandValidator : AbstractValidator<DeleteProductImageCommand>
{
    public DeleteProductImageCommandValidator()
    {
        RuleFor(command => command.ProductImageId)
            .NotEmpty();
    }
}
