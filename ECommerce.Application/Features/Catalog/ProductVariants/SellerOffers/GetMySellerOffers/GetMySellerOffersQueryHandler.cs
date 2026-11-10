using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.GetMySellerOffers;

public sealed class GetMySellerOffersQueryHandler(
    ICurrentUser currentUser,
    ISellerOfferRepository sellerOfferRepository)
    : IRequestHandler<GetMySellerOffersQuery, GetMySellerOffersResponse>
{
    public async Task<GetMySellerOffersResponse> Handle(GetMySellerOffersQuery query,
        CancellationToken cancellationToken)
    {
        var sellerId = currentUser.UserId ?? throw new UnauthorizedException("Authenticated user is required.");

        var totalCount = await sellerOfferRepository.CountBySellerIdAsync(
            sellerId,
            query.Status,
            cancellationToken);

        var offers = await sellerOfferRepository.GetBySellerIdPageAsync(
            sellerId,
            query.Page,
            query.PageSize,
            query.Status,
            cancellationToken);

        var items = offers
            .Select(offer => new SellerOfferListItemResponse(
                offer.Id,
                offer.ProductId,
                offer.ProductName,
                offer.ProductVariantId,
                offer.VariantName,
                offer.SKU,
                offer.Price,
                offer.Stock,
                offer.Status,
                offer.RowVersion,
                offer.CreatedAt))
            .ToList();

        return new GetMySellerOffersResponse(
            items,
            query.Page,
            query.PageSize,
            totalCount);
    }
}
