using MediatR;

namespace ECommerce.Application.Features.Catalog.ProductImages.DeleteProductImage;

public sealed record DeleteProductImageCommand(Guid ProductImageId) : IRequest;
