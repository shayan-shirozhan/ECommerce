using ECommerce.Domain.Enums;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SellerStatus> GetSellerStatusAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SetSellerStatusAsync(Guid userId, SellerStatus status, CancellationToken cancellationToken = default);
}
