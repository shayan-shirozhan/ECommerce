using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Authentication.Login;

public sealed class LoginCommandHandler(
    IIdentityService identityService,
    IAccessTokenService accessTokenService,
    IRefreshTokenService refreshTokenService)
    : IRequestHandler<LoginCommand, LoginResponseDto>
{
    public async Task<LoginResponseDto> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await identityService.AuthenticateAsync(command.Email.Trim(), command.Password, cancellationToken);
        if (user is null)
            throw new UnauthorizedException("Invalid username or password.");

        var accessToken = accessTokenService.Generate(user);
        var refreshToken = await refreshTokenService.IssueAsync(user.Id, cancellationToken);

        return new LoginResponseDto(
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            refreshToken.Token,
            refreshToken.ExpiresAtUtc);
    }
}
