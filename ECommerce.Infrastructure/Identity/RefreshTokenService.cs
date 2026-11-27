using System.Security.Cryptography;
using System.Text;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Identity;

internal sealed class RefreshTokenService(
    ECommerceDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IOptions<RefreshTokenOptions> options,
    TimeProvider timeProvider) : IRefreshTokenService
{
    public async Task<RefreshTokenResult> IssueAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            throw new InvalidOperationException("Cannot issue a refresh token for a user that does not exist.");

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var rawToken = GenerateToken();
        var expiresAtUtc = now.AddDays(options.Value.ExpirationDays);

        dbContext.RefreshTokens.Add(RefreshToken.Create(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            HashToken(rawToken),
            now,
            expiresAtUtc));

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RefreshTokenResult(rawToken, expiresAtUtc);
    }

    public async Task<RefreshTokenRotationResult?> RotateAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;

        var tokenHash = HashToken(refreshToken);
        var currentToken = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (currentToken is null)
            return null;

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // A recently rotated token can be submitted by a second browser tab that started
        // refreshing at the same time. Do not treat that narrow race as a replay while its
        // replacement is still active; the API returns 409 so the client can retry with the
        // cookie set by the winning request. Reuse outside this window remains a replay.
        if (currentToken.RevokedAtUtc.HasValue)
        {
            if (await IsRecentSuccessfulRotationAsync(currentToken.Id, now, cancellationToken))
                throw new RefreshTokenRotationInProgressException();

            await RevokeFamilyAsync(currentToken.FamilyId, now, cancellationToken);
            return null;
        }

        if (currentToken.ExpiresAtUtc <= now)
            return null;

        var user = await userManager.FindByIdAsync(currentToken.UserId.ToString());
        if (user is null || string.IsNullOrWhiteSpace(user.Email) || await userManager.IsLockedOutAsync(user))
        {
            await RevokeFamilyAsync(currentToken.FamilyId, now, cancellationToken);
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        var authenticatedUser = new AuthenticatedUser(user.Id, user.Email, roles.ToArray());

        var rawReplacementToken = GenerateToken();
        var replacementTokenId = Guid.NewGuid();
        var replacementExpiresAtUtc = now.AddDays(options.Value.ExpirationDays);
        var replacementToken = RefreshToken.Create(
            replacementTokenId,
            currentToken.UserId,
            currentToken.FamilyId,
            HashToken(rawReplacementToken),
            now,
            replacementExpiresAtUtc);

        currentToken.Revoke(now, replacementTokenId);
        dbContext.RefreshTokens.Add(replacementToken);

        try
        {
            // The nullable RevokedAtUtc column is a concurrency token. Only one concurrent
            // request can rotate a given refresh token successfully.
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            dbContext.ChangeTracker.Clear();

            if (await IsRecentSuccessfulRotationAsync(currentToken.Id, now, cancellationToken))
                throw new RefreshTokenRotationInProgressException();

            await RevokeFamilyAsync(currentToken.FamilyId, now, cancellationToken);
            return null;
        }

        return new RefreshTokenRotationResult(
            authenticatedUser,
            new RefreshTokenResult(rawReplacementToken, replacementExpiresAtUtc));
    }

    public async Task RevokeAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        var tokenHash = HashToken(refreshToken);
        var familyId = await dbContext.RefreshTokens
            .Where(token => token.TokenHash == tokenHash)
            .Select(token => (Guid?)token.FamilyId)
            .SingleOrDefaultAsync(cancellationToken);

        if (familyId is null)
            return;

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await RevokeFamilyAsync(familyId.Value, now, cancellationToken);
    }

    public async Task RevokeAllForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAtUtc, (DateTime?)now),
                cancellationToken);
    }

    private async Task<bool> IsRecentSuccessfulRotationAsync(
        Guid tokenId,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var gracePeriodSeconds = options.Value.RotationGracePeriodSeconds;
        if (gracePeriodSeconds <= 0)
            return false;

        var previousToken = await dbContext.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(token => token.Id == tokenId, cancellationToken);

        if (previousToken?.RevokedAtUtc is not { } revokedAtUtc ||
            previousToken.ReplacedByTokenId is not { } replacementTokenId ||
            revokedAtUtc < nowUtc.AddSeconds(-gracePeriodSeconds))
        {
            return false;
        }

        // A family-wide revoke (logout, password reset, or confirmed replay) also revokes
        // the replacement, so it must never be mistaken for a concurrent rotation.
        return await dbContext.RefreshTokens
            .AsNoTracking()
            .AnyAsync(token =>
                    token.Id == replacementTokenId &&
                    token.RevokedAtUtc == null &&
                    token.ExpiresAtUtc > nowUtc,
                cancellationToken);
    }

    private Task<int> RevokeFamilyAsync(
        Guid familyId,
        DateTime revokedAtUtc,
        CancellationToken cancellationToken)
    {
        return dbContext.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAtUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAtUtc, (DateTime?)revokedAtUtc),
                cancellationToken);
    }

    private static string GenerateToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashToken(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }
}
