using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Addresses.SetDefaultAddress;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Addresses.SetDefaultAddress;

public sealed class SetDefaultAddressCommandHandlerTests : PostgresTestBase
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
        var otherUsersAddress = CreateAddress(ownerUserId, isDefault: false);
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

        Assert.That(persistedAddress.IsDefault, Is.False);
    }

    [Test]
    public async Task Handle_should_set_selected_address_as_default_and_unset_previous_default()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var currentDefault = CreateAddress(userId, title: "Home", isDefault: true);
        var selectedAddress = CreateAddress(userId, title: "Office");
        var anotherAddress = CreateAddress(userId, title: "Parents");
        await AddUserAndAddressesAsync(userId, currentDefault, selectedAddress, anotherAddress);
        using var provider = CreateProvider(userId);

        // Act
        await SendAsync(provider, CreateCommand(selectedAddress.Id));

        // Assert
        await using var context = CreateDbContext();
        var addresses = await context.Addresses
            .Where(address => address.UserId == userId)
            .ToListAsync();
        var savedCurrentDefault = addresses.Single(address => address.Id == currentDefault.Id);
        var savedSelectedAddress = addresses.Single(address => address.Id == selectedAddress.Id);
        var savedAnotherAddress = addresses.Single(address => address.Id == anotherAddress.Id);

        Assert.Multiple(() =>
        {
            Assert.That(savedCurrentDefault.IsDefault, Is.False);
            Assert.That(savedSelectedAddress.IsDefault, Is.True);
            Assert.That(savedAnotherAddress.IsDefault, Is.False);
            Assert.That(addresses.Count(address => address.IsDefault), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Handle_should_set_default_when_user_does_not_have_a_default_address()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var firstAddress = CreateAddress(userId, title: "Home");
        var selectedAddress = CreateAddress(userId, title: "Office");
        await AddUserAndAddressesAsync(userId, firstAddress, selectedAddress);
        using var provider = CreateProvider(userId);

        // Act
        await SendAsync(provider, CreateCommand(selectedAddress.Id));

        // Assert
        await using var context = CreateDbContext();
        var addresses = await context.Addresses
            .Where(address => address.UserId == userId)
            .ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(addresses.Single(address => address.Id == selectedAddress.Id).IsDefault, Is.True);
            Assert.That(addresses.Count(address => address.IsDefault), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Handle_should_be_idempotent_when_selected_address_is_already_default()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var defaultAddress = CreateAddress(userId, title: "Home", isDefault: true);
        var anotherAddress = CreateAddress(userId, title: "Office");
        await AddUserAndAddressesAsync(userId, defaultAddress, anotherAddress);
        using var provider = CreateProvider(userId);

        // Act
        await SendAsync(provider, CreateCommand(defaultAddress.Id));

        // Assert
        await using var context = CreateDbContext();
        var addresses = await context.Addresses
            .Where(address => address.UserId == userId)
            .ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(addresses.Single(address => address.Id == defaultAddress.Id).IsDefault, Is.True);
            Assert.That(addresses.Single(address => address.Id == anotherAddress.Id).IsDefault, Is.False);
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
            "Test Recipient",
            "09120000000",
            "Tehran",
            "Tehran",
            "1234567890",
            "Test Street 1",
            isDefault);
    }

    private static ApplicationUser CreateUser(Guid id)
    {
        var userName = $"address-default-{id:N}";
        var email = $"{userName}@example.test";

        return new ApplicationUser
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = "Address",
            LastName = "Default Test"
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
        SetDefaultAddressCommand command)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(command);
    }

    private static SetDefaultAddressCommand CreateCommand(Guid addressId)
    {
        return new SetDefaultAddressCommand(addressId);
    }

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }
}
