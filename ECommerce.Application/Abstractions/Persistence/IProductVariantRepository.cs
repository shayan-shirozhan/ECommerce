using ECommerce.Domain.Entities;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IProductVariantRepository
{
    Task<bool> ExistsAsync(Guid productVariantId, CancellationToken cancellationToken = default);
    Task AddAsync(ProductVariant productVariant, CancellationToken cancellationToken =  default);
    Task<ProductVariant?> GetByIdWithDetailsAsync(Guid productVariantId, CancellationToken cancellationToken = default);
    Task<ProductVariant?> GetByIdForUpdateAsync(Guid productVariantId, CancellationToken cancellationToken = default);
}
