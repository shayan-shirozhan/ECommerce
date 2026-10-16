using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.GetSellerOfferById;

public sealed class GetSellerOfferByIdQueryHandler(ISellerOfferRepository sellerOfferRepository)
    : IRequestHandler<GetSellerOfferByIdQuery, SellerOfferDto>
{
    public async Task<SellerOfferDto> Handle(GetSellerOfferByIdQuery query, CancellationToken cancellationToken)
    {
        var offer = await sellerOfferRepository.GetByIdAsync(query.Id, cancellationToken);
        if (offer is null)
            throw new NotFoundException($"Seller offer with id '{query.Id}' was not found.");

        if (offer.Status != SellerOfferStatus.Active || offer.ProductVariant.Status != ProductVariantStatus.Active ||
            offer.ProductVariant.Product.Status != ProductStatus.Active)
            throw new NotFoundException($"Seller offer with id '{query.Id}' was not found.");

        return new SellerOfferDto(
            offer.Id,
            offer.SellerId,
            offer.Price,
            offer.Stock,
            offer.Status,
            offer.RowVersion);
    }
}
