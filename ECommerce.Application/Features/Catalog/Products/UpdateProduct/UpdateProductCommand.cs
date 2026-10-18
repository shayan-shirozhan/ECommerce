using MediatR;

namespace ECommerce.Application.Features.Catalog.Products.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    string Description,
    Guid BrandId,
    Guid CategoryId) : IRequest;
