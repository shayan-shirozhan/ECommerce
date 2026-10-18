using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Authentication.Login;

namespace ECommerce.Tests.Application.Features.Login;

public sealed class LoginCommandTests
{
    [Test]
    public async Task Login_should_return_access_token_when_credentials_are_valid()
    {
        var user = new AuthenticatedUser(
            Guid.NewGuid(),
            "seller@test.com",
            ["Seller"]);

        var identityService = new FakeIdentityService
        {
            User = user
        };

        var accessTokenService = new FakeAccessTokenService
        {
            Result = new AccessTokenResult(
                "access-token",
                DateTime.UtcNow.AddMinutes(60))
        };

        var handler = new LoginCommandHandler(
            identityService,
            accessTokenService);

        var command = new LoginCommand(
            "seller@test.com",
            "Password123!");

        var result = await handler.Handle(
            command,
            CancellationToken.None);

        Assert.That(result, Is.Not.Null);

        Assert.Multiple(() =>
        {
            Assert.That(
                result!.AccessToken,
                Is.EqualTo("access-token"));

            Assert.That(
                result.ExpiresAtUtc,
                Is.EqualTo(accessTokenService.Result.ExpiresAtUtc));

            Assert.That(
                identityService.AuthenticateCalled,
                Is.True);

            Assert.That(
                accessTokenService.GenerateCalled,
                Is.True);

            Assert.That(
                accessTokenService.LastUser,
                Is.SameAs(user));
        });
    }

    [Test]
    public async Task Login_should_throw_unauthorized_when_credentials_are_invalid()
    {
        var identityService = new FakeIdentityService
        {
            User = null
        };

        var accessTokenService = new FakeAccessTokenService();

        var handler = new LoginCommandHandler(
            identityService,
            accessTokenService);

        var command = new LoginCommand(
            "seller@test.com",
            "wrong-password");

        var exception = Assert.ThrowsAsync<UnauthorizedException>(
            async () => await handler.Handle(
                command,
                CancellationToken.None));

        Assert.That(
            exception!.Message,
            Does.Contain("username or password"));

        Assert.That(
            accessTokenService.GenerateCalled,
            Is.False);
    }

    [Test]
    public async Task Login_should_trim_email_before_authentication()
    {
        var identityService = new FakeIdentityService
        {
            User = new AuthenticatedUser(
                Guid.NewGuid(),
                "seller@test.com",
                [])
        };

        var accessTokenService = new FakeAccessTokenService
        {
            Result = new AccessTokenResult(
                "access-token",
                DateTime.UtcNow.AddMinutes(60))
        };

        var handler = new LoginCommandHandler(
            identityService,
            accessTokenService);

        var command = new LoginCommand(
            "  seller@test.com  ",
            "Password123!");

        await handler.Handle(
            command,
            CancellationToken.None);

        Assert.That(
            identityService.LastEmail,
            Is.EqualTo("seller@test.com"));
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        public AuthenticatedUser? User { get; set; }

        public bool AuthenticateCalled { get; private set; }

        public string? LastEmail { get; private set; }

        public string? LastPassword { get; private set; }

        public Task<AuthenticatedUser?> AuthenticateAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default)
        {
            AuthenticateCalled = true;
            LastEmail = email;
            LastPassword = password;

            return Task.FromResult(User);
        }

        public Task<CreatedIdentityUser> CreateUserAsync(
            string email,
            string password,
            string firstName,
            string lastName,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("User creation is not used by login tests.");

        public Task DeleteUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeAccessTokenService : IAccessTokenService
    {
        public AccessTokenResult Result { get; set; } =
            new(
                "default-token",
                DateTime.UtcNow.AddMinutes(60));

        public bool GenerateCalled { get; private set; }

        public AuthenticatedUser? LastUser { get; private set; }

        public AccessTokenResult Generate(AuthenticatedUser user)
        {
            GenerateCalled = true;
            LastUser = user;

            return Result;
        }

        public DateTime ExpiresAtUtc =>
            Result.ExpiresAtUtc;
    }
}