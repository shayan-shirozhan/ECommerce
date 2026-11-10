using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;

namespace ECommerce.Application.Abstractions.Persistence;

public interface ISellerRequestRepository
{
    Task<SellerRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(SellerRequest sellerRequest, CancellationToken cancellationToken = default);
    Task<SellerRequest?> GetPendingByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SellerRequest>> GetPageAsync(int page, int pageSize, SellerRequestStatus? status = null,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(SellerRequestStatus? status = null, CancellationToken cancellationToken = default);
}
