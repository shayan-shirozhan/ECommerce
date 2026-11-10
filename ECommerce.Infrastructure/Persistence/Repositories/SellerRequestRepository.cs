using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

internal sealed class SellerRequestRepository(ECommerceDbContext dbContext) : ISellerRequestRepository
{
    public async Task<SellerRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.SellerRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(SellerRequest sellerRequest, CancellationToken cancellationToken = default)
    {
        await dbContext.SellerRequests.AddAsync(sellerRequest, cancellationToken);
    }

    public async Task<SellerRequest?> GetPendingByUserIdAsync(Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.SellerRequests.FirstOrDefaultAsync(
            x => x.UserId == userId && x.Status == SellerRequestStatus.Pending, cancellationToken);
    }


    public async Task<IReadOnlyList<SellerRequest>> GetPageAsync(int page, int pageSize,
        SellerRequestStatus? status = null, CancellationToken cancellationToken = default)
    {
        var offset = checked((int)(((long)page - 1L) * pageSize));

        var query = dbContext.SellerRequests
            .AsNoTracking();

        if (status.HasValue)
            query = query.Where(request => request.Status == status.Value);

        return await query
            .OrderByDescending(request => request.Id)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAsync(SellerRequestStatus? status = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.SellerRequests
            .AsNoTracking();

        if (status.HasValue)
            query = query.Where(request => request.Status == status.Value);

        return query.CountAsync(cancellationToken);
    }
}
