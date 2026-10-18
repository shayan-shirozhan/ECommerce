using MediatR;

namespace ECommerce.Application.Features.Catalog.Brands.DeleteBrand;

public sealed record DeleteBrandCommand(Guid Id) : IRequest;
