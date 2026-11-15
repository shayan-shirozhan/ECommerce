using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Catalog.ProductVariants.UpdateProductVariant;

public sealed class UpdateProductVariantCommandHandler(
    IProductVariantRepository productVariantRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateProductVariantCommand>
{
    public async Task Handle(
        UpdateProductVariantCommand command,
        CancellationToken cancellationToken)
    {
        var productVariant = await productVariantRepository.GetByIdForUpdateAsync(
            command.Id,
            cancellationToken);

        if (productVariant is null)
            throw new NotFoundException(
                $"Product variant with id '{command.Id}' was not found.");

        var skuExists = await productVariantRepository.ExistsBySkuAsync(
            command.SKU,
            command.Id,
            cancellationToken);

        if (skuExists)
            throw new BusinessRuleException(
                $"A product variant with SKU '{command.SKU}' already exists.");

        productVariant.UpdateDetails(command.SKU, command.Name);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
