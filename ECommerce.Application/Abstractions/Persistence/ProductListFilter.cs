namespace ECommerce.Application.Abstractions.Persistence;

public sealed record ProductListFilter(
    Guid? CategoryId,
    Guid? BrandId,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? Search,
    ProductSortOrder SortOrder);
