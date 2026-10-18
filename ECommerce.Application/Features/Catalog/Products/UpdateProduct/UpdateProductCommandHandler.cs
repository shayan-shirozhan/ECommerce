using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Catalog.Products.UpdateProduct;

public sealed class UpdateProductCommandHandler(
    IProductRepository productRepository,
    IBrandRepository brandRepository,
    ICategoryRepository categoryRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateProductCommand>
{
    public async Task Handle(
        UpdateProductCommand command,
        CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdForUpdateAsync(
            command.Id,
            cancellationToken);

        if (product is null)
            throw new NotFoundException(
                $"Product with id '{command.Id}' was not found.");

        var brandExists = await brandRepository.ExistsAsync(
            command.BrandId,
            cancellationToken);

        if (!brandExists)
            throw new NotFoundException(
                $"Brand with id '{command.BrandId}' was not found.");

        var categoryExists = await categoryRepository.ExistsAsync(
            command.CategoryId,
            cancellationToken);

        if (!categoryExists)
            throw new NotFoundException(
                $"Category with id '{command.CategoryId}' was not found.");

        product.UpdateDetails(
            command.Name,
            command.Description,
            command.BrandId,
            command.CategoryId);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
