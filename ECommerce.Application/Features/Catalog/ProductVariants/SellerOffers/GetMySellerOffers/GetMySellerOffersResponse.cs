using ECommerce.Domain.Enums;

namespace ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.GetMySellerOffers;

public sealed record GetMySellerOffersResponse(
    IReadOnlyList<SellerOfferListItemResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record SellerOfferListItemResponse(
    Guid Id,
    Guid ProductId,
    string ProductName,
    Guid ProductVariantId,
    string? VariantName,
    string SKU,
    decimal Price,
    int Stock,
    SellerOfferStatus Status,
    uint RowVersion,
    DateTime CreatedAt);
