using ECommerce.Infrastructure.Identity;
using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ECommerce.API.BackgroundServices;

public sealed class RefreshTokenCleanupBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<RefreshTokenOptions> options,
    TimeProvider timeProvider,
    ILogger<RefreshTokenCleanupBackgroundService> logger)
    : BackgroundService
{
    private readonly TimeSpan _cleanupInterval =
        TimeSpan.FromHours(options.Value.CleanupIntervalHours);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await CleanupSafelyAsync(stoppingToken);

            using var timer = new PeriodicTimer(_cleanupInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await CleanupSafelyAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Refresh token cleanup background service is stopping.");
        }
    }

    private async Task CleanupSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();
            var retentionCutoffUtc = timeProvider.GetUtcNow().UtcDateTime
                .AddDays(-options.Value.RetentionDays);

            // Retention starts when a token becomes terminal: at revocation when revoked,
            // otherwise at expiration. This preserves replay evidence for the full retention window.
            var deletedCount = await dbContext.RefreshTokens
                .Where(token =>
                    (token.RevokedAtUtc == null && token.ExpiresAtUtc <= retentionCutoffUtc) ||
                    (token.RevokedAtUtc != null && token.RevokedAtUtc <= retentionCutoffUtc))
                .ExecuteDeleteAsync(cancellationToken);

            if (deletedCount > 0)
            {
                logger.LogInformation(
                    "Refresh token cleanup deleted {DeletedCount} records older than the {RetentionDays}-day retention period.",
                    deletedCount,
                    options.Value.RetentionDays);
            }
            else
            {
                logger.LogDebug("Refresh token cleanup completed. No records were eligible for deletion.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Refresh token cleanup failed. The next attempt will run after {IntervalHours} hours.",
                options.Value.CleanupIntervalHours);
        }
    }
}
