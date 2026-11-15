using MediatR;

namespace ECommerce.Application.Features.Catalog.ProductVariants.UpdateProductVariant;

public sealed record UpdateProductVariantCommand(
    Guid Id,
    string SKU,
    string? Name) : IRequest;
