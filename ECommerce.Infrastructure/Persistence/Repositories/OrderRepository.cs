using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository(
    ECommerceDbContext dbContext) : IOrderRepository
{
    public async Task AddAsync(
        Order order,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Orders.AddAsync(
            order,
            cancellationToken);
    }

    public async Task<string?> GetSellerDisplayNameAsync(
        Guid sellerId,
        CancellationToken cancellationToken = default)
    {
        var seller = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == sellerId)
            .Select(user => new
            {
                user.FirstName,
                user.LastName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (seller is null)
            return null;

        var displayName = string.Join(
            " ",
            new[] { seller.FirstName, seller.LastName }
                .Where(name => !string.IsNullOrWhiteSpace(name)));

        return string.IsNullOrWhiteSpace(displayName)
            ? null
            : displayName;
    }

    public async Task<IReadOnlyList<Order>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Orders
            .AsNoTracking()
            .Where(order => order.UserId == userId)
            .Include(order => order.Groups)
            .ThenInclude(group => group.Items)
            .OrderByDescending(order => order.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<Order?> GetByIdForUserAsync(Guid orderId, Guid userId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Orders
            .AsNoTracking()
            .Where(order => order.Id == orderId && order.UserId == userId)
            .Include(order => order.Groups)
            .ThenInclude(group => group.Items)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<Order?> GetByIdForUserForUpdateAsync(Guid orderId, Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.Orders
            .Where(order => order.Id == orderId && order.UserId == userId)
            .Include(order => order.Payments)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
