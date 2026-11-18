using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Users.UpdateMyProfile;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Users.UpdateMyProfile;

public sealed class UpdateMyProfileCommandHandlerTests : PostgresTestBase
{
    [Test]
    public void Handle_should_reject_request_without_authenticated_user_id()
    {
        using var provider = CreateProvider(currentUserId: null);

        Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(provider, CreateCommand()));
    }

    [Test]
    public void Handle_should_return_not_found_when_current_user_does_not_exist()
    {
        using var provider = CreateProvider(Guid.NewGuid());

        Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(provider, CreateCommand()));
    }

    [Test]
    public async Task Handle_should_update_only_current_users_name_and_preserve_account_fields()
    {
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var currentUser = CreateUser(currentUserId, $"profile-update-owner-{suffix}", "Before", "Name");
        currentUser.SellerStatus = SellerStatus.Active;
        var otherUser = CreateUser(otherUserId, $"profile-update-other-{suffix}", "Other", "User");

        await AddUsersAsync(currentUser, otherUser);
        using var provider = CreateProvider(currentUserId);

        await SendAsync(provider, CreateCommand());

        await using var context = CreateDbContext();
        var updatedUser = await context.Users.SingleAsync(user => user.Id == currentUserId);
        var unchangedUser = await context.Users.SingleAsync(user => user.Id == otherUserId);

        Assert.Multiple(() =>
        {
            Assert.That(updatedUser.FirstName, Is.EqualTo("Updated"));
            Assert.That(updatedUser.LastName, Is.EqualTo("Customer"));
            Assert.That(updatedUser.Email, Is.EqualTo(currentUser.Email));
            Assert.That(updatedUser.SellerStatus, Is.EqualTo(SellerStatus.Active));
            Assert.That(unchangedUser.FirstName, Is.EqualTo("Other"));
            Assert.That(unchangedUser.LastName, Is.EqualTo("User"));
        });
    }

    private async Task AddUsersAsync(params ApplicationUser[] users)
    {
        await using var context = CreateDbContext();
        context.Users.AddRange(users);
        await context.SaveChangesAsync();
    }

    private static ApplicationUser CreateUser(
        Guid id,
        string userName,
        string firstName,
        string lastName)
    {
        var email = $"{userName}@example.test";
        return new ApplicationUser
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = firstName,
            LastName = lastName
        };
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

    private static async Task SendAsync(
        IServiceProvider provider,
        UpdateMyProfileCommand command)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(command);
    }

    private static UpdateMyProfileCommand CreateCommand()
    {
        return new UpdateMyProfileCommand(" Updated ", " Customer ");
    }

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }
}
