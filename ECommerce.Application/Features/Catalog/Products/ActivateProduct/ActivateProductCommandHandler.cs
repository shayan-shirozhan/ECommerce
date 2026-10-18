using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Catalog.Products.ActivateProduct;

public sealed class ActivateProductCommandHandler(
    IProductRepository productRepository,
    IBrandRepository brandRepository,
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ActivateProductCommand>
{
    public async Task Handle(
        ActivateProductCommand command,
        CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdForUpdateAsync(
            command.ProductId,
            cancellationToken);

        if (product is null)
            throw new NotFoundException(
                $"Product with id '{command.ProductId}' was not found.");

        var brandExists = await brandRepository.ExistsAsync(
            product.BrandId,
            cancellationToken);

        if (!brandExists)
            throw new BusinessRuleException(
                "The product cannot be activated because its brand does not exist or has been deleted.");

        var categoryExists = await categoryRepository.ExistsAsync(
            product.CategoryId,
            cancellationToken);

        if (!categoryExists)
            throw new BusinessRuleException(
                "The product cannot be activated because its category does not exist or has been deleted.");

        product.Activate();

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
