namespace ECommerce.API.Contracts.Catalog;

public sealed record UpdateProductRequest(
    string Name,
    string Description,
    Guid BrandId,
    Guid CategoryId);
