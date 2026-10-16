using MediatR;

namespace ECommerce.Application.Features.Catalog.ProductVariants.ActivateProductVariant;

public sealed record ActivateProductVariantCommand(Guid ProductVariantId) : IRequest;
