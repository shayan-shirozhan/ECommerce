using MediatR;

namespace ECommerce.Application.Features.Catalog.Products.DeactivateProduct;

public sealed record DeactivateProductCommand(Guid ProductId) : IRequest;
