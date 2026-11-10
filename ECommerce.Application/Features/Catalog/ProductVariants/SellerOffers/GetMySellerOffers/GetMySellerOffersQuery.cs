using ECommerce.Domain.Enums;
using MediatR;

namespace ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.GetMySellerOffers;

public sealed record GetMySellerOffersQuery(int Page = 1, int PageSize = 10, SellerOfferStatus? Status = null)
    : IRequest<GetMySellerOffersResponse>
{
    public const int MaxPageSize = 100;
}
