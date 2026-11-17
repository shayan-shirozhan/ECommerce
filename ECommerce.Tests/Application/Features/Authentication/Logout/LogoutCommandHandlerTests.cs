using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Features.Authentication.Logout;

namespace ECommerce.Tests.Application.Features.Authentication.Logout;

public sealed class LogoutCommandHandlerTests
{
    [Test]
    public async Task Handle_should_revoke_the_supplied_refresh_token()
    {
        var refreshTokenService = new FakeRefreshTokenService();
        var handler = new LogoutCommandHandler(refreshTokenService);

        await handler.Handle(new LogoutCommand("current-refresh-token"), CancellationToken.None);

        Assert.That(refreshTokenService.RevokedToken, Is.EqualTo("current-refresh-token"));
    }

    private sealed class FakeRefreshTokenService : IRefreshTokenService
    {
        public string? RevokedToken { get; private set; }

        public Task<RefreshTokenResult> IssueAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RefreshTokenRotationResult?> RotateAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            RevokedToken = refreshToken;
            return Task.CompletedTask;
        }
    }
}
