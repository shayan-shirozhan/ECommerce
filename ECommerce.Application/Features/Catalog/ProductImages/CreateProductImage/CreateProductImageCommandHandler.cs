using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Domain.Entities;
using MediatR;

namespace ECommerce.Application.Features.Catalog.ProductImages.CreateProductImage;

public class CreateProductImageCommandHandler(
    IProductRepository productRepository,
    IProductVariantRepository productVariantRepository,
    IProductImageRepository productImageRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateProductImageCommand, Guid>
{
    public async Task<Guid> Handle(CreateProductImageCommand command, CancellationToken cancellationToken)
    {
        var productExists = await productRepository.ExistsAsync(command.ProductId, cancellationToken);
        if (!productExists)
            throw new NotFoundException($"Product with id '{command.ProductId}' was not found.");

        if (command.ProductVariantId is Guid productVariantId)
        {
            var variant = await productVariantRepository.GetByIdWithDetailsAsync(
                productVariantId,
                cancellationToken);

            if (variant is null)
                throw new NotFoundException($"Product variant with id '{productVariantId}' was not found.");

            if (variant.ProductId != command.ProductId)
                throw new BusinessRuleException("Product variant must belong to the specified product.");
        }

        var productImage = ProductImage.Create(
            command.ProductId,
            command.Url,
            command.AltText,
            command.SortOrder,
            command.IsPrimary,
            command.ProductVariantId);

        await productImageRepository.AddAsync(productImage, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return productImage.Id;
    }
}