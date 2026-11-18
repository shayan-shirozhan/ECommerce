using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Authentication.Refresh;

namespace ECommerce.Tests.Application.Features.Authentication.Refresh;

public sealed class RefreshTokenCommandHandlerTests
{
    [Test]
    public async Task Handle_should_return_a_new_access_token_when_refresh_token_is_valid()
    {
        var user = new AuthenticatedUser(Guid.NewGuid(), "customer@test.com", ["Customer"]);
        var refreshTokenService = new FakeRefreshTokenService
        {
            Result = new RefreshTokenRotationResult(
                user,
                new RefreshTokenResult("replacement-refresh-token", DateTime.UtcNow.AddDays(30)))
        };
        var accessTokenService = new FakeAccessTokenService();
        var handler = new RefreshTokenCommandHandler(refreshTokenService, accessTokenService);

        var result = await handler.Handle(
            new RefreshTokenCommand("current-refresh-token"),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.AccessToken, Is.EqualTo("new-access-token"));
            Assert.That(result.RefreshToken, Is.EqualTo("replacement-refresh-token"));
            Assert.That(refreshTokenService.LastToken, Is.EqualTo("current-refresh-token"));
            Assert.That(accessTokenService.LastUser, Is.SameAs(user));
        });
    }

    [Test]
    public void Handle_should_throw_unauthorized_when_refresh_token_is_invalid()
    {
        var handler = new RefreshTokenCommandHandler(
            new FakeRefreshTokenService(),
            new FakeAccessTokenService());

        var exception = Assert.ThrowsAsync<UnauthorizedException>(async () =>
            await handler.Handle(new RefreshTokenCommand("invalid-token"), CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("invalid, expired, or has been revoked"));
    }

    private sealed class FakeRefreshTokenService : IRefreshTokenService
    {
        public RefreshTokenRotationResult? Result { get; init; }
        public string? LastToken { get; private set; }

        public Task<RefreshTokenResult> IssueAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RefreshTokenRotationResult?> RotateAsync(
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            LastToken = refreshToken;
            return Task.FromResult(Result);
        }

        public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeAccessTokenService : IAccessTokenService
    {
        public AuthenticatedUser? LastUser { get; private set; }

        public AccessTokenResult Generate(AuthenticatedUser user)
        {
            LastUser = user;
            return new AccessTokenResult("new-access-token", DateTime.UtcNow.AddMinutes(15));
        }
    }
}
