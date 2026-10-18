using FluentValidation;

namespace ECommerce.Application.Features.Catalog.Products.UpdateProduct;

public sealed class UpdateProductCommandValidator
    : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(250);

        RuleFor(command => command.Description)
            .NotEmpty();

        RuleFor(command => command.BrandId)
            .NotEmpty();

        RuleFor(command => command.CategoryId)
            .NotEmpty();
    }
}
