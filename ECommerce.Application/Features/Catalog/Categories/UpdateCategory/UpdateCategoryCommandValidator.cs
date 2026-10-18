using FluentValidation;

namespace ECommerce.Application.Features.Catalog.Categories.UpdateCategory;

public sealed class UpdateCategoryCommandValidator
    : AbstractValidator<UpdateCategoryCommand>
{
    public UpdateCategoryCommandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(150);

        RuleFor(command => command.ParentCategoryId)
            .Must((command, parentCategoryId) =>
                !parentCategoryId.HasValue
                || parentCategoryId.Value == Guid.Empty
                || parentCategoryId.Value != command.Id)
            .WithMessage("A category cannot be its own parent.");
    }
}
