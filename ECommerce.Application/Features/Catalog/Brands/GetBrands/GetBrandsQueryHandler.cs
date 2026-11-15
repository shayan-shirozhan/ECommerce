using ECommerce.Application.Abstractions.Persistence;
using MediatR;

namespace ECommerce.Application.Features.Catalog.Brands.GetBrands;

public sealed class GetBrandsQueryHandler(IBrandRepository brandRepository)
    : IRequestHandler<GetBrandsQuery, GetBrandsResponse>
{
    public async Task<GetBrandsResponse> Handle(
        GetBrandsQuery query,
        CancellationToken cancellationToken)
    {
        var brands = await brandRepository.GetAllAsync(cancellationToken);

        var items = brands
            .Select(brand => new BrandListItemDto(
                brand.Id,
                brand.Name))
            .ToList();

        return new GetBrandsResponse(items);
    }
}
