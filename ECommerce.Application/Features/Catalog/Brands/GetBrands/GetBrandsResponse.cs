namespace ECommerce.Application.Features.Catalog.Brands.GetBrands;

public sealed record GetBrandsResponse(
    IReadOnlyList<BrandListItemDto> Items);
