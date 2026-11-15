using MediatR;

namespace ECommerce.Application.Features.Catalog.Brands.GetBrands;

public sealed record GetBrandsQuery : IRequest<GetBrandsResponse>;
