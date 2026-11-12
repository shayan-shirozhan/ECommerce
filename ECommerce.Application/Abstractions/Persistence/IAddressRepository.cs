using ECommerce.Domain.Entities;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IAddressRepository
{
    Task<IReadOnlyList<Address>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Address>> GetTrackedByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<Address?> GetByIdForUserAsync(
        Guid addressId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Address address,
        CancellationToken cancellationToken = default);

    void Remove(Address address);
}
