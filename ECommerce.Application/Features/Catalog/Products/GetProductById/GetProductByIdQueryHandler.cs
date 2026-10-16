using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Catalog.Brands.GetBrandById;
using ECommerce.Application.Features.Catalog.Categories.GetCategoryById;
using ECommerce.Application.Features.Catalog.ProductImages.GetProductImageById;
using ECommerce.Application.Features.Catalog.ProductVariants.GetProductVariantById;
using ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.GetSellerOfferById;
using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Catalog.Products.GetProductById;

public sealed class GetProductByIdQueryHandler(IProductRepository productRepository)
    : IRequestHandler<GetProductByIdQuery, ProductDetailsDto>
{
    public async Task<ProductDetailsDto> Handle(GetProductByIdQuery query, CancellationToken cancellationToken)
    {
        var product = await productRepository.GetByIdWithDetailsAsync(query.ProductId, cancellationToken);
        if (product is null || product.Status != ProductStatus.Active)
            throw new NotFoundException($"Product with id '{query.ProductId}' was not found.");

        var activeVariantIds = product.Variants
            .Where(variant => variant.Status == ProductVariantStatus.Active)
            .Select(variant => variant.Id)
            .ToHashSet();

        return new ProductDetailsDto(product.Id, product.Name, product.Description, product.Status,
            new BrandDto(product.Brand!.Id, product.Brand.Name),
            new CategoryDto(product.Category.Id, product.Category.Name),
            product.Variants.Where(variant => variant.Status == ProductVariantStatus.Active).Select(variant =>
                new ProductVariantDto(variant.Id, variant.ProductId, variant.SKU, variant.Name, variant.Status,
                    variant.SellerOffers.Where(offer => offer.Status == SellerOfferStatus.Active).Select(offer =>
                        new SellerOfferDto(offer.Id, offer.SellerId, offer.Price, offer.Stock, offer.Status,
                            offer.RowVersion)).ToList())).ToList(),
            product.Images
                .Where(image => image.ProductVariantId is null || activeVariantIds.Contains(image.ProductVariantId.Value))
                .OrderBy(image => image.SortOrder)
                .Select(image => new ProductImageDto(image.Id, image.Url, image.AltText, image.SortOrder,
                    image.IsPrimary, image.ProductVariantId))
                .ToList());
    }
}
