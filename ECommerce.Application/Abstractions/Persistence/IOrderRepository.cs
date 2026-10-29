using ECommerce.Domain.Entities;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken cancellationToken = default);
    Task<string?> GetSellerDisplayNameAsync(Guid sellerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Order>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Order?> GetByIdForUserAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);
    Task<Order?> GetByIdForUserForUpdateAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default);
}
