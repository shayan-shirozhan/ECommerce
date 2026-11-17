using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Authentication.Refresh;

public sealed class RefreshTokenCommandHandler(
    IRefreshTokenService refreshTokenService,
    IAccessTokenService accessTokenService)
    : IRequestHandler<RefreshTokenCommand, RefreshTokenResponseDto>
{
    public async Task<RefreshTokenResponseDto> Handle(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        var rotation = await refreshTokenService.RotateAsync(command.RefreshToken, cancellationToken);
        if (rotation is null)
            throw new UnauthorizedException("The refresh token is invalid, expired, or has been revoked.");

        var accessToken = accessTokenService.Generate(rotation.User);

        return new RefreshTokenResponseDto(
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            rotation.RefreshToken.Token,
            rotation.RefreshToken.ExpiresAtUtc);
    }
}
