namespace ECommerce.Application.Abstractions.Persistence;

public sealed record ProductListingItem(
    Guid Id,
    string Name,
    string Description,
    Guid BrandId,
    string BrandName,
    Guid CategoryId,
    string CategoryName,
    DateTime CreatedAt,
    decimal? Price);
