using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository(ECommerceDbContext dbContext) : IOrderRepository
{
    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        await dbContext.Orders.AddAsync(order, cancellationToken);
    }

    public async Task<string?> GetSellerDisplayNameAsync(
        Guid sellerId,
        CancellationToken cancellationToken = default)
    {
        var seller = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == sellerId)
            .Select(user => new { user.FirstName, user.LastName })
            .FirstOrDefaultAsync(cancellationToken);

        if (seller is null)
            return null;

        var displayName = string.Join(" ", new[] { seller.FirstName, seller.LastName }
            .Where(name => !string.IsNullOrWhiteSpace(name)));

        return string.IsNullOrWhiteSpace(displayName) ? null : displayName;
    }
}
