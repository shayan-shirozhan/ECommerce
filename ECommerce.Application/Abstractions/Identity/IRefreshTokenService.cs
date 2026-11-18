namespace ECommerce.Application.Abstractions.Identity;

public interface IRefreshTokenService
{
    Task<RefreshTokenResult> IssueAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<RefreshTokenRotationResult?> RotateAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public sealed record RefreshTokenResult(string Token, DateTime ExpiresAtUtc);

public sealed record RefreshTokenRotationResult(
    AuthenticatedUser User,
    RefreshTokenResult RefreshToken);
