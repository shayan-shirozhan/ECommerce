using ECommerce.Application.Features.Catalog.Brands.GetBrandById;
using ECommerce.Application.Features.Catalog.Categories.GetCategoryById;

namespace ECommerce.Application.Features.Catalog.Products.GetProducts;

public sealed record ProductListItemDto(
    Guid Id,
    string Name,
    string Description,
    BrandDto Brand,
    CategoryDto Category,
    DateTime CreatedAt,
    decimal? Price);
