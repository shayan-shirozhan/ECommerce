using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Catalog.ProductVariants.DeactivateProductVariant;

public sealed class DeactivateProductVariantCommandHandler(
    IProductVariantRepository productVariantRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeactivateProductVariantCommand>
{
    public async Task Handle(DeactivateProductVariantCommand command, CancellationToken cancellationToken)
    {
        var productVariant = await productVariantRepository.GetByIdForUpdateAsync(
            command.ProductVariantId,
            cancellationToken);

        if (productVariant is null)
            throw new NotFoundException($"Product variant with id '{command.ProductVariantId}' was not found.");

        productVariant.Deactivate();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
