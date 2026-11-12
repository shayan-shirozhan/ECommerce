using ECommerce.Domain.Entities;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IAddressRepository
{
    Task<IReadOnlyList<Address>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
