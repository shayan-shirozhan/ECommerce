using ECommerce.Domain.Enums;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IUserRepository
{
    Task<UserProfileData?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpdateProfileAsync(
        Guid userId,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<SellerStatus> GetSellerStatusAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SetSellerStatusAsync(Guid userId, SellerStatus status, CancellationToken cancellationToken = default);
}
