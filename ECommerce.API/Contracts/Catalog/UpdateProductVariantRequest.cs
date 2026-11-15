namespace ECommerce.API.Contracts.Catalog;

public sealed record UpdateProductVariantRequest(
    string SKU,
    string? Name);
