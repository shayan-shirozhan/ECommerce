namespace ECommerce.Application.Features.Catalog.Categories.GetCategories;

public sealed record GetCategoriesResponse(
    IReadOnlyList<CategoryListItemDto> Items);
