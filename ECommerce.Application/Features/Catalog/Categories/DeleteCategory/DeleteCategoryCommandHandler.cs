using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Catalog.Categories.DeleteCategory;

public sealed class DeleteCategoryCommandHandler(
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteCategoryCommand>
{
    public async Task Handle(
        DeleteCategoryCommand command,
        CancellationToken cancellationToken)
    {
        var category = await categoryRepository.GetByIdForUpdateAsync(
            command.Id,
            cancellationToken);

        if (category is null)
            throw new NotFoundException(
                $"Category with id '{command.Id}' was not found.");

        var hasActiveProducts =
            await categoryRepository.HasActiveProductsAsync(
                category.Id,
                cancellationToken);

        if (hasActiveProducts)
            throw new BusinessRuleException(
                "A category with active products cannot be deleted.");

        var hasChildren = await categoryRepository.HasChildrenAsync(
            category.Id,
            cancellationToken);

        if (hasChildren)
            throw new BusinessRuleException(
                "A category with non-deleted child categories cannot be deleted.");

        category.SoftDelete();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
