using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Catalog.Products.ActivateProduct;

public sealed class ActivateProductCommandHandler(
    IProductRepository productRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<ActivateProductCommand>
{
    public async Task Handle(ActivateProductCommand command, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdForUpdateAsync(command.ProductId, cancellationToken);
        if (product is null)
            throw new NotFoundException($"Product with id '{command.ProductId}' was not found.");

        product.Activate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
