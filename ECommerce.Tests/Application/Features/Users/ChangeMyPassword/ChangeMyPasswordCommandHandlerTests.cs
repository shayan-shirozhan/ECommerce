using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Users.ChangeMyPassword;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Users.ChangeMyPassword;

public sealed class ChangeMyPasswordCommandHandlerTests : PostgresTestBase
{
    private const string CurrentPassword = "Password123!";
    private const string NewPassword = "NewPassword123!";

    [Test]
    public void Handle_should_reject_request_without_authenticated_user_id()
    {
        using var provider = CreateProvider(currentUserId: null);

        Assert.ThrowsAsync<UnauthorizedException>(async () =>
            await SendAsync(provider, CreateCommand()));
    }

    [Test]
    public void Handle_should_return_not_found_when_current_user_does_not_exist()
    {
        using var provider = CreateProvider(Guid.NewGuid());

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await SendAsync(provider, CreateCommand()));
    }

    [Test]
    public async Task Handle_should_change_password_and_revoke_all_refresh_sessions_for_only_current_user()
    {
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        using var provider = CreateProvider(currentUserId);

        await CreateUserAndSessionsAsync(provider, currentUserId, sessionCount: 2);
        await CreateUserAndSessionsAsync(provider, otherUserId, sessionCount: 1);

        await SendAsync(provider, CreateCommand());

        await using var context = CreateDbContext();
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var changedUser = await userManager.FindByIdAsync(currentUserId.ToString());
        var otherUser = await userManager.FindByIdAsync(otherUserId.ToString());
        Assert.That(changedUser, Is.Not.Null);
        Assert.That(otherUser, Is.Not.Null);
        var newPasswordAccepted = await userManager.CheckPasswordAsync(changedUser!, NewPassword);
        var oldPasswordAccepted = await userManager.CheckPasswordAsync(changedUser!, CurrentPassword);
        var currentUserTokens = await context.RefreshTokens
            .Where(token => token.UserId == currentUserId)
            .ToListAsync();
        var otherUserTokens = await context.RefreshTokens
            .Where(token => token.UserId == otherUserId)
            .ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(changedUser, Is.Not.Null);
            Assert.That(otherUser, Is.Not.Null);
            Assert.That(newPasswordAccepted, Is.True);
            Assert.That(oldPasswordAccepted, Is.False);
            Assert.That(currentUserTokens, Has.Count.EqualTo(2));
            Assert.That(currentUserTokens.All(token => token.RevokedAtUtc.HasValue), Is.True);
            Assert.That(otherUserTokens, Has.Count.EqualTo(1));
            Assert.That(otherUserTokens[0].RevokedAtUtc, Is.Null);
        });
    }

    [Test]
    public async Task Handle_should_preserve_password_and_refresh_sessions_when_current_password_is_incorrect()
    {
        var userId = Guid.NewGuid();
        using var provider = CreateProvider(userId);
        await CreateUserAndSessionsAsync(provider, userId, sessionCount: 1);

        var exception = Assert.ThrowsAsync<ValidationException>(async () =>
            await SendAsync(provider, new ChangeMyPasswordCommand("WrongPassword123!", NewPassword)));

        await using var context = CreateDbContext();
        var token = await context.RefreshTokens.SingleAsync(entity => entity.UserId == userId);
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        Assert.That(user, Is.Not.Null);
        var currentPasswordAccepted = await userManager.CheckPasswordAsync(user!, CurrentPassword);

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Errors.Single().PropertyName, Is.EqualTo("currentPassword"));
            Assert.That(token.RevokedAtUtc, Is.Null);
            Assert.That(user, Is.Not.Null);
            Assert.That(currentPasswordAccepted, Is.True);
        });
    }

    [Test]
    public async Task Handle_should_preserve_password_and_refresh_sessions_when_new_password_fails_identity_policy()
    {
        var userId = Guid.NewGuid();
        using var provider = CreateProvider(userId);
        await CreateUserAndSessionsAsync(provider, userId, sessionCount: 1);

        var exception = Assert.ThrowsAsync<ValidationException>(async () =>
            await SendAsync(provider, new ChangeMyPasswordCommand(CurrentPassword, "weak")));

        await using var context = CreateDbContext();
        var token = await context.RefreshTokens.SingleAsync(entity => entity.UserId == userId);
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByIdAsync(userId.ToString());
        Assert.That(user, Is.Not.Null);
        var currentPasswordAccepted = await userManager.CheckPasswordAsync(user!, CurrentPassword);

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Errors, Is.Not.Empty);
            Assert.That(exception.Errors.All(error => error.PropertyName == "newPassword"), Is.True);
            Assert.That(token.RevokedAtUtc, Is.Null);
            Assert.That(user, Is.Not.Null);
            Assert.That(currentPasswordAccepted, Is.True);
        });
    }

    private async Task CreateUserAndSessionsAsync(IServiceProvider provider, Guid userId, int sessionCount)
    {
        using var scope = provider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var refreshTokenService = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var suffix = userId.ToString("N");
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = $"change-password-{suffix}@example.test",
            Email = $"change-password-{suffix}@example.test",
            FirstName = "Test",
            LastName = "User"
        };

        var createResult = await userManager.CreateAsync(user, CurrentPassword);
        Assert.That(createResult.Succeeded, Is.True, string.Join("; ", createResult.Errors.Select(error => error.Description)));

        for (var index = 0; index < sessionCount; index++)
            await refreshTokenService.IssueAsync(userId);
    }

    private ServiceProvider CreateProvider(Guid? currentUserId)
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
        services.AddScoped<ICurrentUser>(_ => new TestCurrentUser(currentUserId));

        return services.BuildServiceProvider();
    }

    private static async Task SendAsync(IServiceProvider provider, ChangeMyPasswordCommand command)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(command);
    }

    private static ChangeMyPasswordCommand CreateCommand()
        => new(CurrentPassword, NewPassword);

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }
}
