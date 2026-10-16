using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(ECommerceDbContext dbContext) : IUserRepository
{
    public async Task<bool> ExistsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Users.AnyAsync(x => x.Id == userId, cancellationToken);
    }

    public async Task<SellerStatus> GetSellerStatusAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var status = await dbContext.Users
            .Where(x => x.Id == userId)
            .Select(x => (SellerStatus?)x.SellerStatus)
            .SingleOrDefaultAsync(cancellationToken);

        return status ?? throw new NotFoundException($"User with id '{userId}' was not found.");
    }

    public async Task SetSellerStatusAsync(Guid userId, SellerStatus status,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
            throw new NotFoundException($"User with id '{userId}' was not found.");

        user.SellerStatus = status;
    }
}
