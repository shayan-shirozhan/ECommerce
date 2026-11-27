using ECommerce.API.Options;
using ECommerce.Infrastructure.Identity;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace ECommerce.API.BackgroundServices;

public sealed class DemoDataResetBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<DemoResetOptions> resetOptions,
    ILogger<DemoDataResetBackgroundService> logger)
    : BackgroundService
{
    private readonly TimeSpan _interval =
        TimeSpan.FromSeconds(resetOptions.Value.IntervalSeconds);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Demo data reset worker started. The reset interval is {IntervalSeconds} seconds.",
            _interval.TotalSeconds);

        using var timer = new PeriodicTimer(_interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await ResetDemoDataAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "The scheduled Demo data reset failed. The next attempt will run after {IntervalSeconds} seconds.",
                        _interval.TotalSeconds);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Demo data reset worker is stopping.");
        }
    }

    private async Task ResetDemoDataAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ECommerceDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var result = await DemoDataResetter.ResetAsync(
            dbContext,
            userManager,
            cancellationToken);

        logger.LogInformation(
            "Demo data reset completed. Deleted {DeletedUsers} visitor users, {DeletedOrders} orders, " +
            "{DeletedPayments} payments, {DeletedPaymentAudits} payment audits, {DeletedCarts} carts, " +
            "{DeletedWishlists} wishlists, {DeletedAddresses} addresses, {DeletedSellerRequests} seller requests, " +
            "{DeletedRefreshTokens} refresh tokens, and {DeletedVisitorSellerOffers} visitor seller offers. " +
            "Restored stock on {RestoredSeedOffers} seeded offers.",
            result.DeletedUsers,
            result.DeletedOrders,
            result.DeletedPayments,
            result.DeletedPaymentAudits,
            result.DeletedCarts,
            result.DeletedWishlists,
            result.DeletedAddresses,
            result.DeletedSellerRequests,
            result.DeletedRefreshTokens,
            result.DeletedVisitorSellerOffers,
            result.RestoredSeedOffers);
    }
}
