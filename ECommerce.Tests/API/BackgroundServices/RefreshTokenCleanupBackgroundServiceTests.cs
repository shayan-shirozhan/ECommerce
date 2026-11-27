using ECommerce.API.BackgroundServices;
using ECommerce.Infrastructure.Identity;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Tests.API.BackgroundServices;

public sealed class RefreshTokenCleanupBackgroundServiceTests : PostgresTestBase
{
    [Test]
    public async Task Worker_should_delete_only_terminal_tokens_past_the_retention_window()
    {
        var timeProvider = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var userId = Guid.NewGuid();
        Guid recentlyExpiredId;
        Guid recentlyRevokedId;
        Guid activeId;

        await using (var setupDbContext = CreateDbContext())
        {
            setupDbContext.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = "refresh-cleanup@test.com",
                NormalizedUserName = "REFRESH-CLEANUP@TEST.COM",
                Email = "refresh-cleanup@test.com",
                NormalizedEmail = "REFRESH-CLEANUP@TEST.COM",
                EmailConfirmed = true,
                FirstName = "Refresh",
                LastName = "Cleanup",
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString()
            });
            await setupDbContext.SaveChangesAsync();

            var expiredBeyondRetention = RefreshToken.Create(
                Guid.NewGuid(), userId, Guid.NewGuid(), new string('a', 64),
                nowUtc.AddDays(-60), nowUtc.AddDays(-31));

            var revokedBeyondRetention = RefreshToken.Create(
                Guid.NewGuid(), userId, Guid.NewGuid(), new string('b', 64),
                nowUtc.AddDays(-60), nowUtc.AddDays(60));
            revokedBeyondRetention.Revoke(nowUtc.AddDays(-31));

            var recentlyExpired = RefreshToken.Create(
                Guid.NewGuid(), userId, Guid.NewGuid(), new string('c', 64),
                nowUtc.AddDays(-59), nowUtc.AddDays(-29));

            var recentlyRevoked = RefreshToken.Create(
                Guid.NewGuid(), userId, Guid.NewGuid(), new string('d', 64),
                nowUtc.AddDays(-60), nowUtc.AddDays(60));
            recentlyRevoked.Revoke(nowUtc.AddDays(-29));

            var active = RefreshToken.Create(
                Guid.NewGuid(), userId, Guid.NewGuid(), new string('e', 64),
                nowUtc, nowUtc.AddDays(30));

            recentlyExpiredId = recentlyExpired.Id;
            recentlyRevokedId = recentlyRevoked.Id;
            activeId = active.Id;

            setupDbContext.RefreshTokens.AddRange(
                expiredBeyondRetention,
                revokedBeyondRetention,
                recentlyExpired,
                recentlyRevoked,
                active);
            await setupDbContext.SaveChangesAsync();
        }

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ECommerceDbContext>(options => options.UseNpgsql(ConnectionString));
        services.AddSingleton<TimeProvider>(timeProvider);
        await using var serviceProvider = services.BuildServiceProvider();

        var worker = new RefreshTokenCleanupBackgroundService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new RefreshTokenOptions
            {
                RetentionDays = 30,
                CleanupIntervalHours = 24
            }),
            timeProvider,
            serviceProvider.GetRequiredService<ILogger<RefreshTokenCleanupBackgroundService>>());

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await WaitForCleanupAsync();
        }
        finally
        {
            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await worker.StopAsync(stopTimeout.Token);
        }

        await using var verificationDbContext = CreateDbContext();
        var remainingTokenIds = await verificationDbContext.RefreshTokens
            .Select(token => token.Id)
            .ToListAsync();

        Assert.That(remainingTokenIds, Is.EquivalentTo(new[]
        {
            recentlyExpiredId,
            recentlyRevokedId,
            activeId
        }));
    }

    private async Task WaitForCleanupAsync()
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var verificationDbContext = CreateDbContext();
            var remainingCount = await verificationDbContext.RefreshTokens.CountAsync();
            if (remainingCount == 3)
                return;

            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }

        Assert.Fail("Refresh token cleanup did not complete within the expected time.");
    }

    private sealed class MutableTimeProvider(DateTimeOffset initialUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => initialUtc;
    }
}
