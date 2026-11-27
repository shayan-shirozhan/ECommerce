using System.Security.Cryptography;
using System.Text;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Tests.Infrastructure.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Infrastructure.Identity;

public sealed class RefreshTokenServiceTests : PostgresTestBase
{
    [Test]
    public async Task Issue_should_persist_only_the_hash_of_a_new_refresh_token()
    {
        using var provider = CreateIdentityServiceProvider();
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var refreshTokenService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        var user = await CreateUserAsync(identityService);
        var issued = await refreshTokenService.IssueAsync(user.Id);
        var persisted = await dbContext.RefreshTokens.SingleAsync(token => token.UserId == user.Id);
        var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(issued.Token)));

        Assert.Multiple(() =>
        {
            Assert.That(issued.Token, Is.Not.Empty);
            Assert.That(issued.Token, Does.Not.Contain("="));
            Assert.That(persisted.TokenHash, Is.EqualTo(expectedHash));
            Assert.That(persisted.TokenHash, Is.Not.EqualTo(issued.Token));
            Assert.That(persisted.UserId, Is.EqualTo(user.Id));
            Assert.That(persisted.FamilyId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(persisted.ExpiresAtUtc, Is.GreaterThan(persisted.CreatedAtUtc));
            Assert.That(persisted.RevokedAtUtc, Is.Null);
        });
    }

    [Test]
    public async Task Rotate_should_revoke_the_previous_token_and_issue_a_replacement_in_the_same_family()
    {
        using var provider = CreateIdentityServiceProvider();
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var refreshTokenService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        var user = await CreateUserAsync(identityService);
        var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>("Customer"));
        Assert.That(roleResult.Succeeded, Is.True);
        var persistedUser = await userManager.FindByIdAsync(user.Id.ToString());
        Assert.That(persistedUser, Is.Not.Null);
        var addRoleResult = await userManager.AddToRoleAsync(persistedUser!, "Customer");
        Assert.That(addRoleResult.Succeeded, Is.True);

        var original = await refreshTokenService.IssueAsync(user.Id);
        var originalRecord = await dbContext.RefreshTokens.SingleAsync(token => token.UserId == user.Id);

        var rotation = await refreshTokenService.RotateAsync(original.Token);
        Assert.That(rotation, Is.Not.Null);

        var originalAfterRotation = await dbContext.RefreshTokens
            .AsNoTracking()
            .SingleAsync(token => token.Id == originalRecord.Id);
        var replacementHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(rotation!.RefreshToken.Token)));
        var replacement = await dbContext.RefreshTokens
            .AsNoTracking()
            .SingleAsync(token => token.TokenHash == replacementHash);

        Assert.Multiple(() =>
        {
            Assert.That(rotation, Is.Not.Null);
            Assert.That(rotation!.User.Id, Is.EqualTo(user.Id));
            Assert.That(rotation.User.Roles, Does.Contain("Customer"));
            Assert.That(rotation.RefreshToken.Token, Is.Not.EqualTo(original.Token));
            Assert.That(originalAfterRotation.RevokedAtUtc, Is.Not.Null);
            Assert.That(originalAfterRotation.ReplacedByTokenId, Is.EqualTo(replacement.Id));
            Assert.That(replacement.FamilyId, Is.EqualTo(originalRecord.FamilyId));
            Assert.That(replacement.RevokedAtUtc, Is.Null);
        });
    }

    [Test]
    public async Task Reusing_a_revoked_token_after_the_grace_window_should_revoke_all_tokens_in_its_family()
    {
        var timeProvider = new MutableTimeProvider(DateTimeOffset.UtcNow);
        using var provider = CreateIdentityServiceProvider(timeProvider);
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var refreshTokenService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        var user = await CreateUserAsync(identityService);
        var original = await refreshTokenService.IssueAsync(user.Id);
        var rotation = await refreshTokenService.RotateAsync(original.Token);
        Assert.That(rotation, Is.Not.Null);

        timeProvider.Advance(TimeSpan.FromSeconds(6));
        var replayResult = await refreshTokenService.RotateAsync(original.Token);
        var familyId = await dbContext.RefreshTokens
            .Where(token => token.TokenHash == Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(original.Token))))
            .Select(token => token.FamilyId)
            .SingleAsync();
        var activeTokensInFamily = await dbContext.RefreshTokens
            .CountAsync(token => token.FamilyId == familyId && token.RevokedAtUtc == null);

        Assert.Multiple(() =>
        {
            Assert.That(replayResult, Is.Null);
            Assert.That(activeTokensInFamily, Is.Zero);
        });
    }

    [Test]
    public async Task Reusing_a_recently_rotated_token_within_the_grace_window_should_not_revoke_its_family()
    {
        using var provider = CreateIdentityServiceProvider();
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var refreshTokenService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        var user = await CreateUserAsync(identityService);
        var original = await refreshTokenService.IssueAsync(user.Id);
        var rotation = await refreshTokenService.RotateAsync(original.Token);
        Assert.That(rotation, Is.Not.Null);

        var exception = Assert.ThrowsAsync<RefreshTokenRotationInProgressException>(async () =>
            await refreshTokenService.RotateAsync(original.Token));

        var originalHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(original.Token)));
        var familyId = await dbContext.RefreshTokens
            .Where(token => token.TokenHash == originalHash)
            .Select(token => token.FamilyId)
            .SingleAsync();
        var activeTokensInFamily = await dbContext.RefreshTokens
            .CountAsync(token => token.FamilyId == familyId && token.RevokedAtUtc == null);

        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(activeTokensInFamily, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Revoke_should_revoke_only_the_supplied_session_family()
    {
        using var provider = CreateIdentityServiceProvider();
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var refreshTokenService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        var user = await CreateUserAsync(identityService);
        var firstSession = await refreshTokenService.IssueAsync(user.Id);
        var secondSession = await refreshTokenService.IssueAsync(user.Id);

        await refreshTokenService.RevokeAsync(firstSession.Token);
        await refreshTokenService.RevokeAsync(firstSession.Token);

        var firstHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(firstSession.Token)));
        var secondHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secondSession.Token)));
        var firstRecord = await dbContext.RefreshTokens.AsNoTracking()
            .SingleAsync(token => token.TokenHash == firstHash);
        var secondRecord = await dbContext.RefreshTokens.AsNoTracking()
            .SingleAsync(token => token.TokenHash == secondHash);

        Assert.Multiple(() =>
        {
            Assert.That(firstRecord.RevokedAtUtc, Is.Not.Null);
            Assert.That(secondRecord.RevokedAtUtc, Is.Null);
            Assert.That(firstRecord.FamilyId, Is.Not.EqualTo(secondRecord.FamilyId));
        });
    }

    [Test]
    public async Task Revoke_should_revoke_the_entire_family_when_called_with_an_already_rotated_token()
    {
        using var provider = CreateIdentityServiceProvider();
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var refreshTokenService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        var user = await CreateUserAsync(identityService);
        var original = await refreshTokenService.IssueAsync(user.Id);
        var rotation = await refreshTokenService.RotateAsync(original.Token);
        Assert.That(rotation, Is.Not.Null);

        // Logout may race with rotation and arrive carrying the now-revoked previous cookie.
        await refreshTokenService.RevokeAsync(original.Token);

        var familyId = await dbContext.RefreshTokens
            .Where(token => token.TokenHash == Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(original.Token))))
            .Select(token => token.FamilyId)
            .SingleAsync();
        var activeTokensInFamily = await dbContext.RefreshTokens
            .CountAsync(token => token.FamilyId == familyId && token.RevokedAtUtc == null);

        Assert.That(activeTokensInFamily, Is.Zero);
    }

    [Test]
    public async Task Rotate_should_revoke_family_when_user_is_locked_out()
    {
        using var provider = CreateIdentityServiceProvider();
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var refreshTokenService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        var user = await CreateUserAsync(identityService);
        var persistedUser = await userManager.FindByIdAsync(user.Id.ToString());
        Assert.That(persistedUser, Is.Not.Null);

        var enableLockoutResult = await userManager.SetLockoutEnabledAsync(persistedUser!, true);
        Assert.That(enableLockoutResult.Succeeded, Is.True);
        var lockoutResult = await userManager.SetLockoutEndDateAsync(
            persistedUser!, DateTimeOffset.UtcNow.AddMinutes(5));
        Assert.That(lockoutResult.Succeeded, Is.True);
        Assert.That(await userManager.IsLockedOutAsync(persistedUser!), Is.True);

        var issued = await refreshTokenService.IssueAsync(user.Id);
        var result = await refreshTokenService.RotateAsync(issued.Token);
        var tokenHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(issued.Token)));
        var persistedToken = await dbContext.RefreshTokens.AsNoTracking()
            .SingleAsync(token => token.TokenHash == tokenHash);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            Assert.That(persistedToken.RevokedAtUtc, Is.Not.Null);
        });
    }

    [Test]
    public async Task Concurrent_rotation_attempts_should_allow_one_winner_without_revoking_its_replacement()
    {
        using var provider = CreateIdentityServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var identityService = firstScope.ServiceProvider.GetRequiredService<IIdentityService>();
        var firstRefreshTokenService = firstScope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var secondRefreshTokenService = secondScope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

        var user = await CreateUserAsync(identityService);
        var original = await firstRefreshTokenService.IssueAsync(user.Id);

        var rotations = await Task.WhenAll(
            TryRotateAsync(firstRefreshTokenService, original.Token),
            TryRotateAsync(secondRefreshTokenService, original.Token));

        using var verificationScope = provider.CreateScope();
        var dbContext = verificationScope.ServiceProvider.GetRequiredService<ECommerceDbContext>();
        var originalHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(original.Token)));
        var familyId = await dbContext.RefreshTokens
            .Where(token => token.TokenHash == originalHash)
            .Select(token => token.FamilyId)
            .SingleAsync();
        var activeTokensInFamily = await dbContext.RefreshTokens
            .CountAsync(token => token.FamilyId == familyId && token.RevokedAtUtc == null);

        Assert.Multiple(() =>
        {
            Assert.That(rotations.Count(rotation => rotation.Result is not null), Is.EqualTo(1));
            Assert.That(rotations.Count(rotation => rotation.Exception is RefreshTokenRotationInProgressException), Is.EqualTo(1));
            Assert.That(activeTokensInFamily, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Rotate_should_return_null_for_an_expired_token()
    {
        using var provider = CreateIdentityServiceProvider();
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var refreshTokenService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        var user = await CreateUserAsync(identityService);
        var issued = await refreshTokenService.IssueAsync(user.Id);
        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(issued.Token)));

        await dbContext.RefreshTokens
            .Where(token => token.TokenHash == tokenHash)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                token => token.ExpiresAtUtc,
                DateTime.UtcNow.AddMinutes(-1)));

        // Force subsequent queries to reload the updated database values.
        dbContext.ChangeTracker.Clear();

        var result = await refreshTokenService.RotateAsync(issued.Token);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task Rotate_should_return_null_for_an_unknown_token()
    {
        using var provider = CreateIdentityServiceProvider();
        using var scope = provider.CreateScope();
        var refreshTokenService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

        var result = await refreshTokenService.RotateAsync("not-a-valid-refresh-token");

        Assert.That(result, Is.Null);
    }

    private ServiceProvider CreateIdentityServiceProvider(TimeProvider? timeProvider = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ECommerceDb"] = ConnectionString,
                ["Jwt:Issuer"] = "ECommerce.Tests",
                ["Jwt:Audience"] = "ECommerce.Tests",
                ["Jwt:SecretKey"] = new string('S', 64),
                ["RefreshTokens:ExpirationDays"] = "30"
            })
            .Build();

        services.AddInfrastructure(configuration);
        if (timeProvider is not null)
            services.AddSingleton(timeProvider);

        return services.BuildServiceProvider();
    }

    private static async Task<(RefreshTokenRotationResult? Result, Exception? Exception)> TryRotateAsync(
        IRefreshTokenService refreshTokenService,
        string token)
    {
        try
        {
            return (await refreshTokenService.RotateAsync(token), null);
        }
        catch (Exception exception)
        {
            return (null, exception);
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset initialUtc) : TimeProvider
    {
        private DateTimeOffset _utcNow = initialUtc;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan amount) => _utcNow = _utcNow.Add(amount);
    }

    private static Task<CreatedIdentityUser> CreateUserAsync(IIdentityService identityService)
    {
        return identityService.CreateUserAsync(
            $"refresh-{Guid.NewGuid():N}@test.com",
            "Password123!",
            "Refresh",
            "Token",
            CancellationToken.None);
    }
}
