using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Addresses.CreateAddress;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Addresses.CreateAddress;

public sealed class CreateAddressCommandHandlerTests : PostgresTestBase
{
    [Test]
    public void Handle_should_reject_request_without_authenticated_user_id()
    {
        using var provider = CreateProvider(currentUserId: null);

        Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(provider, CreateCommand()));
    }

    [Test]
    public async Task Handle_should_make_first_address_default_automatically()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await AddUserAsync(userId);
        using var provider = CreateProvider(userId);

        // Act - IsDefault is false, but a user's first address must be default.
        var response = await SendAsync(provider, CreateCommand(isDefault: false));

        // Assert
        Assert.That(response.IsDefault, Is.True);

        await using var context = CreateDbContext();
        var savedAddress = await context.Addresses.SingleAsync(address => address.Id == response.Id);
        Assert.That(savedAddress.UserId, Is.EqualTo(userId));
        Assert.That(savedAddress.IsDefault, Is.True);
    }

    [Test]
    public async Task Handle_should_make_new_address_default_when_existing_addresses_have_no_default()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingAddress = Address.Create(
            userId, "Home", "Existing Recipient", "09120000000", "Tehran", "Tehran",
            "1234567890", "Existing Street", isDefault: false);
        await AddUserAndAddressesAsync(userId, existingAddress);
        using var provider = CreateProvider(userId);

        // Act
        var response = await SendAsync(provider, CreateCommand(isDefault: false));

        // Assert
        Assert.That(response.IsDefault, Is.True);

        await using var context = CreateDbContext();
        var addresses = await context.Addresses
            .Where(address => address.UserId == userId)
            .ToListAsync();

        Assert.That(addresses.Count(address => address.IsDefault), Is.EqualTo(1));
        Assert.That(addresses.Single(address => address.Id == existingAddress.Id).IsDefault, Is.False);
        Assert.That(addresses.Single(address => address.Id == response.Id).IsDefault, Is.True);
    }

    [Test]
    public async Task Handle_should_preserve_existing_default_when_new_address_is_not_default()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingDefault = Address.Create(
            userId, "Home", "Existing Recipient", "09120000000", "Tehran", "Tehran",
            "1234567890", "Existing Street", isDefault: true);
        await AddUserAndAddressesAsync(userId, existingDefault);
        using var provider = CreateProvider(userId);

        // Act
        var response = await SendAsync(provider, CreateCommand(isDefault: false));

        // Assert
        Assert.That(response.IsDefault, Is.False);

        await using var context = CreateDbContext();
        var addresses = await context.Addresses
            .Where(address => address.UserId == userId)
            .ToListAsync();

        Assert.That(addresses, Has.Count.EqualTo(2));
        Assert.That(addresses.Single(address => address.Id == existingDefault.Id).IsDefault, Is.True);
        Assert.That(addresses.Single(address => address.Id == response.Id).IsDefault, Is.False);
    }

    [Test]
    public async Task Handle_should_replace_existing_default_when_new_address_is_requested_as_default()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var existingDefault = Address.Create(
            userId, "Home", "Existing Recipient", "09120000000", "Tehran", "Tehran",
            "1234567890", "Existing Street", isDefault: true);
        var existingNonDefault = Address.Create(
            userId, "Office", "Existing Recipient", "09120000000", "Tehran", "Tehran",
            "1234567890", "Office Street");
        await AddUserAndAddressesAsync(userId, existingDefault, existingNonDefault);
        using var provider = CreateProvider(userId);

        // Act
        var response = await SendAsync(provider, CreateCommand(isDefault: true));

        // Assert
        Assert.That(response.IsDefault, Is.True);

        await using var context = CreateDbContext();
        var addresses = await context.Addresses
            .Where(address => address.UserId == userId)
            .ToListAsync();

        Assert.That(addresses.Count(address => address.IsDefault), Is.EqualTo(1));
        Assert.That(addresses.Single(address => address.Id == existingDefault.Id).IsDefault, Is.False);
        Assert.That(addresses.Single(address => address.Id == existingNonDefault.Id).IsDefault, Is.False);
        Assert.That(addresses.Single(address => address.Id == response.Id).IsDefault, Is.True);
    }

    [Test]
    public async Task Handle_should_not_change_other_users_default_address()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var currentDefault = Address.Create(
            currentUserId, "Home", "Current Recipient", "09120000000", "Tehran", "Tehran",
            "1234567890", "Current Street", isDefault: true);
        var otherDefault = Address.Create(
            otherUserId, "Home", "Other Recipient", "09120000000", "Tehran", "Tehran",
            "1234567890", "Other Street", isDefault: true);
        await AddUsersAndAddressesAsync(
            (currentUserId, currentDefault),
            (otherUserId, otherDefault));
        using var provider = CreateProvider(currentUserId);

        // Act
        var response = await SendAsync(provider, CreateCommand(isDefault: true));

        // Assert
        Assert.That(response.IsDefault, Is.True);

        await using var context = CreateDbContext();
        var otherUsersSavedAddress = await context.Addresses
            .SingleAsync(address => address.Id == otherDefault.Id);
        Assert.That(otherUsersSavedAddress.IsDefault, Is.True);

        var currentUsersDefaultAddresses = await context.Addresses
            .Where(address => address.UserId == currentUserId && address.IsDefault)
            .CountAsync();
        Assert.That(currentUsersDefaultAddresses, Is.EqualTo(1));
    }

    [Test]
    public async Task Database_should_reject_more_than_one_default_address_for_same_user()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var firstDefault = Address.Create(
            userId, "Home", "Recipient", "09120000000", "Tehran", "Tehran",
            "1234567890", "First Street", isDefault: true);
        await AddUserAndAddressesAsync(userId, firstDefault);

        await using var context = CreateDbContext();
        var duplicateDefault = Address.Create(
            userId, "Office", "Recipient", "09120000000", "Tehran", "Tehran",
            "1234567890", "Second Street", isDefault: true);
        context.Addresses.Add(duplicateDefault);

        // Act + Assert
        Assert.ThrowsAsync<PersistenceConflictException>(
            async () => await context.SaveChangesAsync());
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
        params (Guid UserId, Address Address)[] usersAndAddresses)
    {
        await using var context = CreateDbContext();

        foreach (var (userId, address) in usersAndAddresses)
        {
            context.Users.Add(CreateUser(userId));
            context.Addresses.Add(address);
        }

        await context.SaveChangesAsync();
    }

    private static ApplicationUser CreateUser(Guid id)
    {
        var userName = $"address-create-{id:N}";
        var email = $"{userName}@example.test";

        return new ApplicationUser
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = "Address",
            LastName = "Create Test"
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

    private static async Task<CreateAddressResponse> SendAsync(
        IServiceProvider provider,
        CreateAddressCommand command)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(command);
    }

    private static CreateAddressCommand CreateCommand(bool isDefault = false)
    {
        return new CreateAddressCommand(
            "Home",
            "Test Recipient",
            "09120000000",
            "Tehran",
            "Tehran",
            "1234567890",
            "Test Street 1",
            isDefault);
    }

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }
}
