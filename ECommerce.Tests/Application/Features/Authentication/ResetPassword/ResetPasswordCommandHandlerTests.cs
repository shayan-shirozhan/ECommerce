using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Authentication.ResetPassword;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Authentication.ResetPassword;

public sealed class ResetPasswordCommandHandlerTests : PostgresTestBase
{
    private const string CurrentPassword = "Password123!";
    private const string NewPassword = "NewPassword123!";

    [Test]
    public async Task Handle_should_reset_password_and_revoke_all_refresh_sessions_for_only_target_user()
    {
        var email = CreateEmail(Guid.NewGuid());
        var otherEmail = CreateEmail(Guid.NewGuid());
        using var provider = CreateProvider();

        var userId = await CreateUserAndSessionsAsync(provider, email, sessionCount: 2);
        var otherUserId = await CreateUserAndSessionsAsync(provider, otherEmail, sessionCount: 1);
        var token = await GenerateTokenAsync(provider, email);

        await SendAsync(provider, new ResetPasswordCommand(email, token, NewPassword));

        using (var scope = provider.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId.ToString());
            Assert.That(user, Is.Not.Null);
            Assert.That(await userManager.CheckPasswordAsync(user!, NewPassword), Is.True);
            Assert.That(await userManager.CheckPasswordAsync(user!, CurrentPassword), Is.False);
        }

        await using (var context = CreateDbContext())
        {
            var targetTokens = await context.RefreshTokens.Where(token => token.UserId == userId).ToListAsync();
            var otherTokens = await context.RefreshTokens.Where(token => token.UserId == otherUserId).ToListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(targetTokens, Has.Count.EqualTo(2));
                Assert.That(targetTokens.All(token => token.RevokedAtUtc.HasValue), Is.True);
                Assert.That(otherTokens, Has.Count.EqualTo(1));
                Assert.That(otherTokens[0].RevokedAtUtc, Is.Null);
            });
        }

        var replayException = Assert.ThrowsAsync<ValidationException>(async () =>
            await SendAsync(provider, new ResetPasswordCommand(email, token, "AnotherPassword123!")));
        Assert.That(replayException!.Errors.All(error => error.PropertyName == "token"), Is.True);
    }

    [Test]
    public async Task Handle_should_preserve_password_and_sessions_when_token_is_invalid()
    {
        var email = CreateEmail(Guid.NewGuid());
        using var provider = CreateProvider();
        var userId = await CreateUserAndSessionsAsync(provider, email, sessionCount: 1);

        var exception = Assert.ThrowsAsync<ValidationException>(async () =>
            await SendAsync(provider, new ResetPasswordCommand(email, "invalid-token", NewPassword)));

        using (var scope = provider.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId.ToString());
            Assert.That(user, Is.Not.Null);
            Assert.That(await userManager.CheckPasswordAsync(user!, CurrentPassword), Is.True);
        }

        await using var context = CreateDbContext();
        var refreshToken = await context.RefreshTokens.SingleAsync(token => token.UserId == userId);

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Errors.Single().PropertyName, Is.EqualTo("token"));
            Assert.That(refreshToken.RevokedAtUtc, Is.Null);
        });
    }

    [Test]
    public async Task Handle_should_preserve_password_and_sessions_when_new_password_fails_identity_policy()
    {
        var email = CreateEmail(Guid.NewGuid());
        using var provider = CreateProvider();
        var userId = await CreateUserAndSessionsAsync(provider, email, sessionCount: 1);
        var token = await GenerateTokenAsync(provider, email);

        var exception = Assert.ThrowsAsync<ValidationException>(async () =>
            await SendAsync(provider, new ResetPasswordCommand(email, token, "weak")));

        using (var scope = provider.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(userId.ToString());
            Assert.That(user, Is.Not.Null);
            Assert.That(await userManager.CheckPasswordAsync(user!, CurrentPassword), Is.True);
        }

        await using var context = CreateDbContext();
        var refreshToken = await context.RefreshTokens.SingleAsync(token => token.UserId == userId);

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Errors, Is.Not.Empty);
            Assert.That(exception.Errors.All(error => error.PropertyName == "newPassword"), Is.True);
            Assert.That(refreshToken.RevokedAtUtc, Is.Null);
        });
    }

    [Test]
    public void Handle_should_return_validation_error_for_unknown_email_without_disclosing_account_existence()
    {
        using var provider = CreateProvider();

        var exception = Assert.ThrowsAsync<ValidationException>(async () =>
            await SendAsync(
                provider,
                new ResetPasswordCommand($"unknown-{Guid.NewGuid():N}@example.test", "some-token", NewPassword)));

        Assert.That(exception!.Errors.Single().PropertyName, Is.EqualTo("token"));
    }

    private async Task<Guid> CreateUserAndSessionsAsync(
        IServiceProvider provider,
        string email,
        int sessionCount)
    {
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var refreshTokenService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

        var createdUser = await identityService.CreateUserAsync(email, CurrentPassword, "Test", "User");

        for (var index = 0; index < sessionCount; index++)
            await refreshTokenService.IssueAsync(createdUser.Id);

        return createdUser.Id;
    }

    private static async Task<string> GenerateTokenAsync(IServiceProvider provider, string email)
    {
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        return (await identityService.GeneratePasswordResetTokenAsync(email))!;
    }

    private static async Task SendAsync(IServiceProvider provider, ResetPasswordCommand command)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(command);
    }

    private ServiceProvider CreateProvider()
    {
        var configuration = new ConfigurationManager();
        configuration["ConnectionStrings:ECommerceDb"] = ConnectionString;
        configuration["Jwt:Issuer"] = "ECommerce.Tests";
        configuration["Jwt:Audience"] = "ECommerce.Tests";
        configuration["Jwt:ExpirationMinutes"] = "60";
        configuration["Jwt:SecretKey"] = new string('t', 32);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }

    private static string CreateEmail(Guid userId) => $"reset-{userId:N}@example.test";
}
