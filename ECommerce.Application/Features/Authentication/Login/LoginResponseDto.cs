namespace ECommerce.Application.Features.Authentication.Login;

public sealed record LoginResponseDto(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);
