using ECommerce.Domain.Entities;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IBrandRepository
{
    Task<bool> ExistsAsync(
        Guid brandId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Brand brand,
        CancellationToken cancellationToken = default);

    Task<Brand?> GetByIdAsync(
        Guid brandId,
        CancellationToken cancellationToken = default);

    Task<Brand?> GetByIdForUpdateAsync(
        Guid brandId,
        CancellationToken cancellationToken = default);

    Task<bool> HasActiveProductsAsync(
        Guid brandId,
        CancellationToken cancellationToken = default);
}
