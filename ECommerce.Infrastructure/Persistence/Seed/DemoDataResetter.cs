using ECommerce.Infrastructure.Identity;
using ECommerce.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Seed;

public static class DemoDataResetter
{
    public static async Task<DemoDataResetResult> ResetAsync(
        ECommerceDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(userManager);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var seededUsers = await userManager.Users
            .Where(user => user.NormalizedEmail == DemoSeedDefaults.AdminNormalizedEmail ||
                           user.NormalizedEmail == DemoSeedDefaults.FirstSellerNormalizedEmail ||
                           user.NormalizedEmail == DemoSeedDefaults.SecondSellerNormalizedEmail ||
                           user.NormalizedEmail == DemoSeedDefaults.CustomerNormalizedEmail)
            .ToListAsync(cancellationToken);

        if (seededUsers.Count != 4)
        {
            throw new InvalidOperationException(
                "The four seeded Demo accounts must exist before Demo data can be reset. Run the Demo seed first.");
        }

        var seededUserIds = seededUsers.Select(user => user.Id).ToArray();
        var firstSellerId = seededUsers.Single(
            user => user.NormalizedEmail == DemoSeedDefaults.FirstSellerNormalizedEmail).Id;
        var secondSellerId = seededUsers.Single(
            user => user.NormalizedEmail == DemoSeedDefaults.SecondSellerNormalizedEmail).Id;

        // Audit rows and payments restrict deletion of their parent order/payment records.
        var deletedPaymentAudits = await dbContext.PaymentResolutionAudits
            .ExecuteDeleteAsync(cancellationToken);
        var deletedPayments = await dbContext.Payments
            .ExecuteDeleteAsync(cancellationToken);
        var deletedOrders = await dbContext.Orders
            .ExecuteDeleteAsync(cancellationToken);

        // Deleting the aggregate roots also deletes their child items through database cascades.
        var deletedCarts = await dbContext.Carts
            .ExecuteDeleteAsync(cancellationToken);
        var deletedWishlists = await dbContext.Wishlists
            .ExecuteDeleteAsync(cancellationToken);

        // These records may belong to the four persistent demo accounts as well as visitor accounts.
        var deletedAddresses = await dbContext.Addresses
            .ExecuteDeleteAsync(cancellationToken);
        var deletedSellerRequests = await dbContext.SellerRequests
            .ExecuteDeleteAsync(cancellationToken);
        var deletedRefreshTokens = await dbContext.RefreshTokens
            .ExecuteDeleteAsync(cancellationToken);

        // Remove offers created by visitor seller accounts. Keep offers owned by the two seeded sellers.
        var seededSellerIds = new[] { firstSellerId, secondSellerId };
        var deletedVisitorSellerOffers = await dbContext.SellerOffers
            .Where(offer => !seededSellerIds.Contains(offer.SellerId))
            .ExecuteDeleteAsync(cancellationToken);

        // ExecuteDeleteAsync bypasses EF's change tracker. Detach stale tracked rows
        // before deleting visitor users; otherwise required/restricted relationships
        // (for example ApplicationUser -> Order) can fail during relationship fixup
        // even though the corresponding database rows have already been deleted.
        dbContext.ChangeTracker.Clear();

        var visitorUsers = await userManager.Users
            .Where(user => !seededUserIds.Contains(user.Id))
            .ToListAsync(cancellationToken);

        foreach (var visitorUser in visitorUsers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var deleteResult = await userManager.DeleteAsync(visitorUser);
            if (deleteResult.Succeeded)
                continue;

            var errors = string.Join("; ", deleteResult.Errors.Select(error => error.Description));
            throw new InvalidOperationException(
                $"Failed to delete a visitor Demo account during reset: {errors}");
        }

        var seededOffers = await dbContext.SellerOffers
            .Where(offer => offer.SellerId == firstSellerId || offer.SellerId == secondSellerId)
            .ToListAsync(cancellationToken);

        foreach (var offer in seededOffers)
        {
            offer.UpdateStock(
                offer.SellerId == firstSellerId
                    ? DemoSeedDefaults.FirstSellerStock
                    : DemoSeedDefaults.SecondSellerStock);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var restoredSeedOffers = seededOffers.Count;

        return new DemoDataResetResult(
            DeletedUsers: visitorUsers.Count,
            DeletedOrders: deletedOrders,
            DeletedPayments: deletedPayments,
            DeletedPaymentAudits: deletedPaymentAudits,
            DeletedCarts: deletedCarts,
            DeletedWishlists: deletedWishlists,
            DeletedAddresses: deletedAddresses,
            DeletedSellerRequests: deletedSellerRequests,
            DeletedRefreshTokens: deletedRefreshTokens,
            DeletedVisitorSellerOffers: deletedVisitorSellerOffers,
            RestoredSeedOffers: restoredSeedOffers);
    }
}

public sealed record DemoDataResetResult(
    int DeletedUsers,
    int DeletedOrders,
    int DeletedPayments,
    int DeletedPaymentAudits,
    int DeletedCarts,
    int DeletedWishlists,
    int DeletedAddresses,
    int DeletedSellerRequests,
    int DeletedRefreshTokens,
    int DeletedVisitorSellerOffers,
    int RestoredSeedOffers);
