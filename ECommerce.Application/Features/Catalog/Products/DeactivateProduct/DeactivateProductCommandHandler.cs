using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Catalog.Products.DeactivateProduct;

public sealed class DeactivateProductCommandHandler(
    IProductRepository productRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeactivateProductCommand>
{
    public async Task Handle(DeactivateProductCommand command, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdForUpdateAsync(command.ProductId, cancellationToken);
        if (product is null)
            throw new NotFoundException($"Product with id '{command.ProductId}' was not found.");

        product.Deactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
