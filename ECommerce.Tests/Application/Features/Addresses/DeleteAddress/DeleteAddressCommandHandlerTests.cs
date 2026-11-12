using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Addresses.DeleteAddress;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Addresses.DeleteAddress;

public sealed class DeleteAddressCommandHandlerTests : PostgresTestBase
{
    [Test]
    public void Handle_should_reject_request_without_authenticated_user_id()
    {
        using var provider = CreateProvider(currentUserId: null);

        Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(provider, new DeleteAddressCommand(Guid.NewGuid())));
    }

    [Test]
    public async Task Handle_should_return_not_found_when_address_does_not_exist()
    {
        var userId = Guid.NewGuid();
        await SeedAsync((userId, Array.Empty<Address>()));
        using var provider = CreateProvider(userId);

        Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(provider, new DeleteAddressCommand(Guid.NewGuid())));
    }

    [Test]
    public async Task Handle_should_return_not_found_when_address_belongs_to_another_user()
    {
        var currentUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var otherUsersAddress = CreateAddress(ownerUserId, isDefault: true);
        await SeedAsync(
            (currentUserId, Array.Empty<Address>()),
            (ownerUserId, new[] { otherUsersAddress }));
        using var provider = CreateProvider(currentUserId);

        Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(provider, new DeleteAddressCommand(otherUsersAddress.Id)));

        await using var context = CreateDbContext();
        var persistedAddress = await context.Addresses
            .SingleAsync(address => address.Id == otherUsersAddress.Id);

        Assert.Multiple(() =>
        {
            Assert.That(persistedAddress.UserId, Is.EqualTo(ownerUserId));
            Assert.That(persistedAddress.IsDefault, Is.True);
        });
    }

    [Test]
    public async Task Handle_should_delete_non_default_address_and_preserve_current_default()
    {
        var userId = Guid.NewGuid();
        var defaultAddress = CreateAddress(userId, title: "Home", isDefault: true);
        var addressToDelete = CreateAddress(userId, title: "Office");
        await SeedAsync((userId, new[] { defaultAddress, addressToDelete }));
        using var provider = CreateProvider(userId);

        await SendAsync(provider, new DeleteAddressCommand(addressToDelete.Id));

        await using var context = CreateDbContext();
        var remainingAddresses = await context.Addresses
            .Where(address => address.UserId == userId)
            .ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(remainingAddresses, Has.Count.EqualTo(1));
            Assert.That(remainingAddresses[0].Id, Is.EqualTo(defaultAddress.Id));
            Assert.That(remainingAddresses[0].IsDefault, Is.True);
        });
    }

    [Test]
    public async Task Handle_should_promote_most_recent_remaining_address_when_default_is_deleted()
    {
        var userId = Guid.NewGuid();
        var defaultAddress = CreateAddress(userId, title: "Home", isDefault: true);
        await Task.Delay(20);
        var olderReplacement = CreateAddress(userId, title: "Office");
        await Task.Delay(20);
        var newestReplacement = CreateAddress(userId, title: "Warehouse");
        await SeedAsync((userId, new[] { defaultAddress, olderReplacement, newestReplacement }));
        using var provider = CreateProvider(userId);

        await SendAsync(provider, new DeleteAddressCommand(defaultAddress.Id));

        await using var context = CreateDbContext();
        var remainingAddresses = await context.Addresses
            .Where(address => address.UserId == userId)
            .ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(remainingAddresses, Has.Count.EqualTo(2));
            Assert.That(remainingAddresses.Any(address => address.Id == defaultAddress.Id), Is.False);
            Assert.That(remainingAddresses.Single(address => address.Id == newestReplacement.Id).IsDefault, Is.True);
            Assert.That(remainingAddresses.Single(address => address.Id == olderReplacement.Id).IsDefault, Is.False);
            Assert.That(remainingAddresses.Count(address => address.IsDefault), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Handle_should_delete_last_address_without_requiring_a_replacement_default()
    {
        var userId = Guid.NewGuid();
        var onlyAddress = CreateAddress(userId, isDefault: true);
        await SeedAsync((userId, new[] { onlyAddress }));
        using var provider = CreateProvider(userId);

        await SendAsync(provider, new DeleteAddressCommand(onlyAddress.Id));

        await using var context = CreateDbContext();
        var remainingAddresses = await context.Addresses
            .Where(address => address.UserId == userId)
            .ToListAsync();

        Assert.That(remainingAddresses, Is.Empty);
    }

    private async Task SeedAsync(params (Guid UserId, Address[] Addresses)[] usersAndAddresses)
    {
        await using var context = CreateDbContext();

        foreach (var (userId, addresses) in usersAndAddresses)
        {
            context.Users.Add(CreateUser(userId));
            context.Addresses.AddRange(addresses);
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
        var userName = $"address-delete-{id:N}";
        var email = $"{userName}@example.test";

        return new ApplicationUser
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = "Address",
            LastName = "Delete Test"
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
        DeleteAddressCommand command)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(command);
    }

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }
}
