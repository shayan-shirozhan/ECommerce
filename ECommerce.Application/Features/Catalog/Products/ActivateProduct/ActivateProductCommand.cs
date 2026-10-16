using MediatR;

namespace ECommerce.Application.Features.Catalog.Products.ActivateProduct;

public sealed record ActivateProductCommand(Guid ProductId) : IRequest;
