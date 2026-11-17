namespace ECommerce.Application.Features.Authentication.Refresh;

public sealed record RefreshTokenResponseDto(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);
