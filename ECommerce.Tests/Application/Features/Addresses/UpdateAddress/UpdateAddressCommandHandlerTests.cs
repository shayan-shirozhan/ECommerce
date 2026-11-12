using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Addresses.UpdateAddress;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Addresses.UpdateAddress;

public sealed class UpdateAddressCommandHandlerTests : PostgresTestBase
{
    [Test]
    public void Handle_should_reject_request_without_authenticated_user_id()
    {
        using var provider = CreateProvider(currentUserId: null);

        Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(provider, CreateCommand(Guid.NewGuid())));
    }

    [Test]
    public async Task Handle_should_return_not_found_when_address_does_not_exist()
    {
        var userId = Guid.NewGuid();
        await AddUserAsync(userId);
        using var provider = CreateProvider(userId);

        Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(provider, CreateCommand(Guid.NewGuid())));
    }

    [Test]
    public async Task Handle_should_return_not_found_when_address_belongs_to_another_user()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var otherUsersAddress = CreateAddress(ownerUserId, isDefault: true);
        await AddUsersAndAddressesAsync(
            (currentUserId, null),
            (ownerUserId, otherUsersAddress));
        using var provider = CreateProvider(currentUserId);

        // Act + Assert
        Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(provider, CreateCommand(otherUsersAddress.Id)));

        await using var context = CreateDbContext();
        var persistedAddress = await context.Addresses
            .SingleAsync(address => address.Id == otherUsersAddress.Id);

        Assert.Multiple(() =>
        {
            Assert.That(persistedAddress.Title, Is.EqualTo("Home"));
            Assert.That(persistedAddress.IsDefault, Is.True);
        });
    }

    [Test]
    public async Task Handle_should_update_all_address_fields_and_preserve_default_status()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var address = CreateAddress(userId, isDefault: true);
        await AddUserAndAddressesAsync(userId, address);
        using var provider = CreateProvider(userId);

        // Act
        await SendAsync(provider, CreateCommand(address.Id));

        // Assert
        await using var context = CreateDbContext();
        var updatedAddress = await context.Addresses
            .SingleAsync(item => item.Id == address.Id);

        Assert.Multiple(() =>
        {
            Assert.That(updatedAddress.UserId, Is.EqualTo(userId));
            Assert.That(updatedAddress.Title, Is.EqualTo("Office Updated"));
            Assert.That(updatedAddress.RecipientName, Is.EqualTo("Updated Recipient"));
            Assert.That(updatedAddress.PhoneNumber, Is.EqualTo("09121111111"));
            Assert.That(updatedAddress.Province, Is.EqualTo("Alborz"));
            Assert.That(updatedAddress.City, Is.EqualTo("Karaj"));
            Assert.That(updatedAddress.PostalCode, Is.EqualTo("9876543210"));
            Assert.That(updatedAddress.AddressLine, Is.EqualTo("Updated Street 2"));
            Assert.That(updatedAddress.IsDefault, Is.True);
            Assert.That(updatedAddress.UpdatedAt, Is.Not.Null);
        });
    }

    [Test]
    public async Task Handle_should_preserve_non_default_status_and_leave_existing_default_unchanged()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var defaultAddress = CreateAddress(userId, title: "Home", isDefault: true);
        var addressToUpdate = CreateAddress(userId, title: "Office", isDefault: false);
        await AddUserAndAddressesAsync(userId, defaultAddress, addressToUpdate);
        using var provider = CreateProvider(userId);

        // Act
        await SendAsync(provider, CreateCommand(addressToUpdate.Id));

        // Assert
        await using var context = CreateDbContext();
        var addresses = await context.Addresses
            .Where(address => address.UserId == userId)
            .ToListAsync();
        var savedDefault = addresses.Single(address => address.Id == defaultAddress.Id);
        var savedUpdated = addresses.Single(address => address.Id == addressToUpdate.Id);

        Assert.Multiple(() =>
        {
            Assert.That(savedDefault.IsDefault, Is.True);
            Assert.That(savedDefault.Title, Is.EqualTo("Home"));
            Assert.That(savedUpdated.IsDefault, Is.False);
            Assert.That(savedUpdated.Title, Is.EqualTo("Office Updated"));
            Assert.That(addresses.Count(address => address.IsDefault), Is.EqualTo(1));
        });
    }

    private async Task AddUserAsync(Guid userId)
    {
        await using var context = CreateDbContext();
        context.Users.Add(CreateUser(userId));
        await context.SaveChangesAsync();
    }

    private async Task AddUserAndAddressesAsync(Guid userId, params Address[] addresses)
    {
        await using var context = CreateDbContext();
        context.Users.Add(CreateUser(userId));
        context.Addresses.AddRange(addresses);
        await context.SaveChangesAsync();
    }

    private async Task AddUsersAndAddressesAsync(
        params (Guid UserId, Address? Address)[] usersAndAddresses)
    {
        await using var context = CreateDbContext();

        foreach (var (userId, address) in usersAndAddresses)
        {
            context.Users.Add(CreateUser(userId));
            if (address is not null)
                context.Addresses.Add(address);
        }

        await context.SaveChangesAsync();
    }

    private static Address CreateAddress(
        Guid userId,
        string title = "Home",
        bool isDefault = false)
    {
        return Address.Create(
            userId,
            title,
            "Original Recipient",
            "09120000000",
            "Tehran",
            "Tehran",
            "1234567890",
            "Original Street 1",
            isDefault);
    }

    private static ApplicationUser CreateUser(Guid id)
    {
        var userName = $"address-update-{id:N}";
        var email = $"{userName}@example.test";

        return new ApplicationUser
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = "Address",
            LastName = "Update Test"
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

    private static async Task SendAsync(
        IServiceProvider provider,
        UpdateAddressCommand command)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(command);
    }

    private static UpdateAddressCommand CreateCommand(Guid addressId)
    {
        return new UpdateAddressCommand(
            addressId,
            "Office Updated",
            "Updated Recipient",
            "09121111111",
            "Alborz",
            "Karaj",
            "9876543210",
            "Updated Street 2");
    }

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }
}
