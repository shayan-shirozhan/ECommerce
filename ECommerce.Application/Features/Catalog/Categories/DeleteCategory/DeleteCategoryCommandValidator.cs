using FluentValidation;

namespace ECommerce.Application.Features.Catalog.Categories.DeleteCategory;

public sealed class DeleteCategoryCommandValidator
    : AbstractValidator<DeleteCategoryCommand>
{
    public DeleteCategoryCommandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();
    }
}
