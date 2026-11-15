namespace ECommerce.Application.Features.Catalog.Categories.GetCategories;

public sealed record CategoryListItemDto(
    Guid Id,
    string Name,
    Guid? ParentCategoryId);
