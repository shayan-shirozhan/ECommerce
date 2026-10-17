using ECommerce.Domain.Entities;

namespace ECommerce.Application.Abstractions.Persistence;

public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken cancellationToken = default);
    Task<string?> GetSellerDisplayNameAsync(Guid sellerId, CancellationToken cancellationToken = default);
}
