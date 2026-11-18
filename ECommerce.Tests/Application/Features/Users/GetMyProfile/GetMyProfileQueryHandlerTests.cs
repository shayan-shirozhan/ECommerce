using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Users.GetMyProfile;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Users.GetMyProfile;

public sealed class GetMyProfileQueryHandlerTests : PostgresTestBase
{
    [Test]
    public void Handle_should_reject_request_without_authenticated_user_id()
    {
        using var provider = CreateProvider(currentUserId: null);

        Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(provider));
    }

    [Test]
    public void Handle_should_return_not_found_when_current_user_does_not_exist()
    {
        using var provider = CreateProvider(Guid.NewGuid());

        Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(provider));
    }

    [Test]
    public async Task Handle_should_return_only_the_current_users_profile()
    {
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var currentUser = CreateUser(currentUserId, $"profile-owner-{suffix}", "Current", "User");
        var otherUser = CreateUser(otherUserId, $"profile-other-{suffix}", "Other", "User");
        otherUser.SellerStatus = SellerStatus.Active;

        await AddUsersAsync(currentUser, otherUser);
        using var provider = CreateProvider(currentUserId);

        var response = await SendAsync(provider);

        Assert.Multiple(() =>
        {
            Assert.That(response.Id, Is.EqualTo(currentUserId));
            Assert.That(response.Email, Is.EqualTo(currentUser.Email));
            Assert.That(response.FirstName, Is.EqualTo("Current"));
            Assert.That(response.LastName, Is.EqualTo("User"));
            Assert.That(response.SellerStatus, Is.EqualTo(SellerStatus.None));
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

    private static async Task<GetMyProfileResponse> SendAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new GetMyProfileQuery());
    }

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }
}
