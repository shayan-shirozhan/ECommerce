using ECommerce.Domain.Entities;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IProductRepository
{
    Task<Product?> GetByIdWithDetailsAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Product>> GetActivePageAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveAsync(
        CancellationToken cancellationToken = default);

    Task<Product?> GetByIdForUpdateAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Product product,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        Guid productId,
        CancellationToken cancellationToken = default);
}
