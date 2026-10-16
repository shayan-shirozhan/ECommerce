using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Catalog.ProductImages.GetProductImageById;

public sealed class GetProductImageByIdQueryHandler(IProductImageRepository productImageRepository)
    : IRequestHandler<GetProductImageByIdQuery, ProductImageDto>
{
    public async Task<ProductImageDto> Handle(GetProductImageByIdQuery query, CancellationToken cancellationToken)
    {
        var image = await productImageRepository.GetByIdAsync(query.Id, cancellationToken);
        if (image is null || !IsPubliclyVisible(image))
            throw new NotFoundException($"Product image with id '{query.Id}' was not found.");

        return new ProductImageDto(
            image.Id,
            image.Url,
            image.AltText,
            image.SortOrder,
            image.IsPrimary,
            image.ProductVariantId);
    }

    private static bool IsPubliclyVisible(ProductImage image)
    {
        if (image.Product is null || image.Product.Status != ProductStatus.Active)
            return false;

        if (image.ProductVariantId is null)
            return true;

        var variant = image.ProductVariant;
        return variant is not null &&
               variant.Id == image.ProductVariantId.Value &&
               variant.ProductId == image.ProductId &&
               variant.Status == ProductVariantStatus.Active &&
               variant.Product is not null &&
               variant.Product.Status == ProductStatus.Active;
    }
}
