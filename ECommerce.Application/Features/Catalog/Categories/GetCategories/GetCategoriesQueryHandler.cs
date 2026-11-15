using ECommerce.Application.Abstractions.Persistence;
using MediatR;

namespace ECommerce.Application.Features.Catalog.Categories.GetCategories;

public sealed class GetCategoriesQueryHandler(ICategoryRepository categoryRepository)
    : IRequestHandler<GetCategoriesQuery, GetCategoriesResponse>
{
    public async Task<GetCategoriesResponse> Handle(
        GetCategoriesQuery query,
        CancellationToken cancellationToken)
    {
        var categories = await categoryRepository.GetAllAsync(cancellationToken);

        var items = categories
            .Select(category => new CategoryListItemDto(
                category.Id,
                category.Name,
                category.ParentCategoryId))
            .ToList();

        return new GetCategoriesResponse(items);
    }
}
