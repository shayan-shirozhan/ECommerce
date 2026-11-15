namespace ECommerce.Application.Abstractions.Persistence;

public sealed record CategoryListingItem(
    Guid Id,
    string Name,
    Guid? ParentCategoryId);
