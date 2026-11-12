using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Repositories;

internal sealed class AddressRepository(ECommerceDbContext dbContext) : IAddressRepository
{
    public async Task<IReadOnlyList<Address>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Addresses
            .AsNoTracking()
            .Where(address => address.UserId == userId)
            .OrderByDescending(address => address.IsDefault)
            .ThenByDescending(address => address.CreatedAt)
            .ThenBy(address => address.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Address>> GetTrackedByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Addresses
            .Where(address => address.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Address?> GetByIdForUserAsync(
        Guid addressId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Addresses
            .FirstOrDefaultAsync(
                address => address.Id == addressId && address.UserId == userId,
                cancellationToken);
    }

    public async Task AddAsync(
        Address address,
        CancellationToken cancellationToken = default)
    {
        await dbContext.Addresses.AddAsync(address, cancellationToken);
    }

    public void Remove(Address address)
    {
        dbContext.Addresses.Remove(address);
    }
}
