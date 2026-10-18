using MediatR;

namespace ECommerce.Application.Features.Catalog.Brands.UpdateBrand;

public sealed record UpdateBrandCommand(
    Guid Id,
    string Name) : IRequest;
