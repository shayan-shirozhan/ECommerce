namespace ECommerce.API.Contracts.Catalog;

public sealed record UpdateCategoryRequest(
    string Name,
    Guid? ParentCategoryId);
