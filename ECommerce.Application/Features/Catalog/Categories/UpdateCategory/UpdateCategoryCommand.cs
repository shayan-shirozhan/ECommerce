using MediatR;

namespace ECommerce.Application.Features.Catalog.Categories.UpdateCategory;

public sealed record UpdateCategoryCommand(
    Guid Id,
    string Name,
    Guid? ParentCategoryId) : IRequest;
