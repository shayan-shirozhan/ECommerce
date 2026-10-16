using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Catalog.ProductVariants.ActivateProductVariant;

public sealed class ActivateProductVariantCommandHandler(
    IProductVariantRepository productVariantRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<ActivateProductVariantCommand>
{
    public async Task Handle(ActivateProductVariantCommand command, CancellationToken cancellationToken)
    {
        var productVariant = await productVariantRepository.GetByIdForUpdateAsync(
            command.ProductVariantId,
            cancellationToken);

        if (productVariant is null)
            throw new NotFoundException($"Product variant with id '{command.ProductVariantId}' was not found.");

        productVariant.Activate(productVariant.Product);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
