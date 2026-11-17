using ECommerce.Application.Abstractions.Identity;
using MediatR;

namespace ECommerce.Application.Features.Authentication.Logout;

public sealed class LogoutCommandHandler(IRefreshTokenService refreshTokenService)
    : IRequestHandler<LogoutCommand, Unit>
{
    public async Task<Unit> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        await refreshTokenService.RevokeAsync(command.RefreshToken, cancellationToken);
        return Unit.Value;
    }
}
