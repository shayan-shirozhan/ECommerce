using MediatR;

namespace ECommerce.Application.Features.Catalog.Categories.DeleteCategory;

public sealed record DeleteCategoryCommand(Guid Id) : IRequest;
