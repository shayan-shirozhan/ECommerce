using ECommerce.Domain.Entities;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IProductImageRepository
{
    Task AddAsync(ProductImage productImage, CancellationToken cancellationToken = default);
    Task<ProductImage?> GetByIdAsync(Guid productImageId, CancellationToken cancellationToken = default);
    Task<ProductImage?> GetByIdForUpdateAsync(Guid productImageId, CancellationToken cancellationToken = default);
    void Remove(ProductImage productImage);
}