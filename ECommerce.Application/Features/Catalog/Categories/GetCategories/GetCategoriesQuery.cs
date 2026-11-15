using MediatR;

namespace ECommerce.Application.Features.Catalog.Categories.GetCategories;

public sealed record GetCategoriesQuery : IRequest<GetCategoriesResponse>;
