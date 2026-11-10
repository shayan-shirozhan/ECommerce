using ECommerce.Domain.Enums;

namespace ECommerce.Application.Abstractions.Persistence;

public sealed record SellerOfferListingItem(
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
