using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Addresses.GetMyAddresses;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Addresses.GetMyAddresses;

public sealed class GetMyAddressesQueryHandlerTests : PostgresTestBase
{
    [Test]
    public void Handle_should_reject_request_without_authenticated_user_id()
    {
        // Arrange
        using var provider = CreateProvider(currentUserId: null);

        // Act + Assert
        Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(provider));
    }

    [Test]
    public async Task Handle_should_return_empty_addresses_when_user_has_no_addresses()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var provider = CreateProvider(userId);

        // Act
        var response = await SendAsync(provider);

        // Assert
        Assert.That(response.Addresses, Is.Empty);
    }

    [Test]
    public async Task Handle_should_return_only_addresses_owned_by_current_user()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var currentUserAddress = CreateAddress(currentUserId, "Home", isDefault: true);
        var otherUserAddress = CreateAddress(otherUserId, "Office", isDefault: true);

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(currentUserId, $"address-owner-{suffix}"),
                CreateUser(otherUserId, $"other-address-owner-{suffix}"));
            context.Addresses.AddRange(currentUserAddress, otherUserAddress);

            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(currentUserId);

        // Act
        var response = await SendAsync(provider);

        // Assert
        Assert.That(response.Addresses, Has.Count.EqualTo(1));
        Assert.That(response.Addresses.Single().Id, Is.EqualTo(currentUserAddress.Id));
        Assert.That(response.Addresses.Single().Title, Is.EqualTo("Home"));
        Assert.That(response.Addresses.Single().IsDefault, Is.True);
    }

    [Test]
    public async Task Handle_should_return_default_address_first_then_non_default_addresses_newest_first()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var olderNonDefaultAddress = CreateAddress(currentUserId, "Older non-default");
        var defaultAddress = CreateAddress(currentUserId, "Default", isDefault: true);
        var newestNonDefaultAddress = CreateAddress(currentUserId, "Newest non-default");
        var otherUsersAddress = CreateAddress(otherUserId, "Other user's default", isDefault: true);

        var olderCreatedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var defaultCreatedAt = new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc);
        var newestCreatedAt = new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc);

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(currentUserId, $"address-owner-{suffix}"),
                CreateUser(otherUserId, $"other-address-owner-{suffix}"));
            context.Addresses.AddRange(
                olderNonDefaultAddress,
                defaultAddress,
                newestNonDefaultAddress,
                otherUsersAddress);

            context.Entry(olderNonDefaultAddress)
                .Property(address => address.CreatedAt)
                .CurrentValue = olderCreatedAt;
            context.Entry(defaultAddress)
                .Property(address => address.CreatedAt)
                .CurrentValue = defaultCreatedAt;
            context.Entry(newestNonDefaultAddress)
                .Property(address => address.CreatedAt)
                .CurrentValue = newestCreatedAt;
            context.Entry(otherUsersAddress)
                .Property(address => address.CreatedAt)
                .CurrentValue = newestCreatedAt.AddDays(1);

            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(currentUserId);

        // Act
        var response = await SendAsync(provider);

        // Assert
        Assert.That(
            response.Addresses.Select(address => address.Id),
            Is.EqualTo(new[]
            {
                defaultAddress.Id,
                newestNonDefaultAddress.Id,
                olderNonDefaultAddress.Id
            }));

        Assert.That(response.Addresses[0].IsDefault, Is.True);
        Assert.That(response.Addresses[1].IsDefault, Is.False);
        Assert.That(response.Addresses[2].IsDefault, Is.False);
        Assert.That(response.Addresses[0].CreatedAt, Is.EqualTo(defaultCreatedAt));
        Assert.That(response.Addresses[1].CreatedAt, Is.EqualTo(newestCreatedAt));
        Assert.That(response.Addresses[2].CreatedAt, Is.EqualTo(olderCreatedAt));
    }

    private static Address CreateAddress(
        Guid userId,
        string title,
        bool isDefault = false)
    {
        return Address.Create(
            userId,
            title,
            "Test Recipient",
            "09120000000",
            "Tehran",
            "Tehran",
            "1234567890",
            "Test Street 1",
            isDefault);
    }

    private static ApplicationUser CreateUser(Guid id, string userName)
    {
        var email = $"{userName}@example.test";

        return new ApplicationUser
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = "Address",
            LastName = "Test"
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
        services.AddScoped<ICurrentUser>(
            _ => new TestCurrentUser(currentUserId));

        return services.BuildServiceProvider();
    }

    private static async Task<GetMyAddressesResponse> SendAsync(
        IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        return await sender.Send(new GetMyAddressesQuery());
    }

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }
}
