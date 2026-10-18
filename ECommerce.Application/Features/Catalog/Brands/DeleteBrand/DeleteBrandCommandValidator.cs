using FluentValidation;

namespace ECommerce.Application.Features.Catalog.Brands.DeleteBrand;

public sealed class DeleteBrandCommandValidator
    : AbstractValidator<DeleteBrandCommand>
{
    public DeleteBrandCommandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();
    }
}
