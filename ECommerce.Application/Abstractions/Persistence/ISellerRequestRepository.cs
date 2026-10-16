using ECommerce.Application.Features.Sellers.SellerRequests.CreateSellerRequest;
using ECommerce.Domain.Entities;

namespace ECommerce.Application.Abstractions.Persistence;

public interface ISellerRequestRepository
{
    Task<SellerRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(SellerRequest sellerRequest, CancellationToken cancellationToken = default);
    Task<SellerRequest?> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
