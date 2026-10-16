using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Catalog.Products.GetProductById;
using ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.GetSellerOfferById;
using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Catalog.ProductVariants.GetProductVariantById;

public sealed class GetProductVariantByIdQueryHandler(IProductVariantRepository productVariantRepository)
    : IRequestHandler<GetProductVariantByIdQuery, ProductVariantDto>
{
    public async Task<ProductVariantDto> Handle(GetProductVariantByIdQuery query, CancellationToken cancellationToken)
    {
        var variant = await productVariantRepository.GetByIdWithDetailsAsync(query.Id, cancellationToken);
        if (variant is null)
            throw new NotFoundException($"Product variant with id '{query.Id}' was not found.");

        if (variant.Status != ProductVariantStatus.Active || variant.Product.Status != ProductStatus.Active)
            throw new NotFoundException($"Product variant with id '{query.Id}' was not found.");

        return new ProductVariantDto(variant.Id, variant.ProductId, variant.SKU, variant.Name, variant.Status,
            variant.SellerOffers.Where(offer => offer.Status == SellerOfferStatus.Active).Select(offer =>
                new SellerOfferDto(offer.Id, offer.SellerId, offer.Price, offer.Stock, offer.Status,
                    offer.RowVersion)).ToList());
    }
}
