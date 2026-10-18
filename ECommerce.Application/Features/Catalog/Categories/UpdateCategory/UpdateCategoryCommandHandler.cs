using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Catalog.Categories.UpdateCategory;

public sealed class UpdateCategoryCommandHandler(
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateCategoryCommand>
{
    public async Task Handle(
        UpdateCategoryCommand command,
        CancellationToken cancellationToken)
    {
        var category = await categoryRepository.GetByIdForUpdateAsync(
            command.Id,
            cancellationToken);

        if (category is null)
            throw new NotFoundException(
                $"Category with id '{command.Id}' was not found.");

        Guid? parentCategoryId =
            command.ParentCategoryId == Guid.Empty
                ? null
                : command.ParentCategoryId;

        if (parentCategoryId.HasValue)
        {
            var parentExists = await categoryRepository.ExistsAsync(
                parentCategoryId.Value,
                cancellationToken);

            if (!parentExists)
                throw new NotFoundException(
                    $"Parent category with id '{parentCategoryId.Value}' was not found.");

            var wouldCreateCycle =
                await categoryRepository.WouldCreateCycleAsync(
                    category.Id,
                    parentCategoryId,
                    cancellationToken);

            if (wouldCreateCycle)
                throw new BusinessRuleException(
                    "A category cannot be moved beneath itself or one of its descendants.");
        }

        category.UpdateDetails(command.Name, parentCategoryId);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
