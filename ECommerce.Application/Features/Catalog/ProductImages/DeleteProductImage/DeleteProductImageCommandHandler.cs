using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Catalog.ProductImages.DeleteProductImage;

public sealed class DeleteProductImageCommandHandler(
    IProductImageRepository productImageRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteProductImageCommand>
{
    public async Task Handle(
        DeleteProductImageCommand command,
        CancellationToken cancellationToken)
    {
        var productImage = await productImageRepository.GetByIdForUpdateAsync(
            command.ProductImageId,
            cancellationToken);

        if (productImage is null)
            throw new NotFoundException(
                $"Product image with id '{command.ProductImageId}' was not found.");

        productImageRepository.Remove(productImage);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
