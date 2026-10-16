using MediatR;

namespace ECommerce.Application.Features.Catalog.ProductVariants.DeactivateProductVariant;

public sealed record DeactivateProductVariantCommand(Guid ProductVariantId) : IRequest;
