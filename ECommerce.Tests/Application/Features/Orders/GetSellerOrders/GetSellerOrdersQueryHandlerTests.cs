using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Orders.GetSellerOrders;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Orders.GetSellerOrders;

public sealed class GetSellerOrdersQueryHandlerTests : PostgresTestBase
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
    public async Task Handle_should_return_empty_orders_when_seller_has_no_orders()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        using var provider = CreateProvider(currentUserId: sellerId);

        // Act
        var response = await SendAsync(provider);

        // Assert
        Assert.That(response.Orders, Is.Empty);
    }

    [Test]
    public async Task Handle_should_return_only_current_sellers_group_from_multi_seller_order()
    {
        // Arrange
        var buyerId = Guid.NewGuid();
        var currentSellerId = Guid.NewGuid();
        var otherSellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var order = CreateOrder(
            buyerId,
            currentSellerId,
            unitPrice: 100m,
            quantity: 2,
            discountAmount: 0m);

        // A second item belongs to the current seller and is part of the same group.
        order.AddItem(
            currentSellerId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Second Current Seller Product",
            "Blue",
            "Current Seller",
            50m,
            2,
            10m);

        // This item creates a separate group belonging to another seller.
        order.AddItem(
            otherSellerId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Other Seller Product",
            "Large",
            "Other Seller",
            100m,
            3,
            25m);

        var currentSellerGroup = order.Groups.Single(
            group => group.SellerId == currentSellerId);
        var otherSellerGroup = order.Groups.Single(
            group => group.SellerId == otherSellerId);

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(
                    buyerId,
                    $"buyer-{suffix}",
                    "Buyer",
                    "Test"),
                CreateUser(
                    currentSellerId,
                    $"current-seller-{suffix}",
                    "Current",
                    "Seller"),
                CreateUser(
                    otherSellerId,
                    $"other-seller-{suffix}",
                    "Other",
                    "Seller"));

            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(currentSellerId);

        // Act
        var response = await SendAsync(provider);

        // Assert: the seller receives one summary for their own group only.
        Assert.That(response.Orders, Has.Count.EqualTo(1));

        var returnedOrder = response.Orders.Single();

        Assert.That(returnedOrder.OrderId, Is.EqualTo(order.Id));
        Assert.That(returnedOrder.OrderStatus, Is.EqualTo(order.Status));
        Assert.That(
            returnedOrder.OrderGroupId,
            Is.EqualTo(currentSellerGroup.Id));
        Assert.That(
            returnedOrder.OrderGroupId,
            Is.Not.EqualTo(otherSellerGroup.Id));
        Assert.That(
            returnedOrder.OrderGroupStatus,
            Is.EqualTo(currentSellerGroup.Status));
        Assert.That(returnedOrder.CreatedAt, Is.Not.EqualTo(default(DateTime)));
        Assert.That(returnedOrder.TotalAmount, Is.EqualTo(290m));
        Assert.That(returnedOrder.TotalAmount, Is.Not.EqualTo(order.TotalAmount));
        Assert.That(returnedOrder.ItemCount, Is.EqualTo(2));
    }

    [Test]
    public async Task Handle_should_return_group_specific_data_for_each_seller()
    {
        // Arrange
        var buyerId = Guid.NewGuid();
        var sellerAId = Guid.NewGuid();
        var sellerBId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var order = CreateOrder(
            buyerId,
            sellerAId,
            unitPrice: 80m,
            quantity: 2,
            discountAmount: 0m);

        order.AddItem(
            sellerBId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Seller B Product",
            "Default",
            "Seller B",
            40m,
            3,
            5m);

        var groupA = order.Groups.Single(group => group.SellerId == sellerAId);
        var groupB = order.Groups.Single(group => group.SellerId == sellerBId);

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(buyerId, $"buyer-{suffix}", "Buyer", "Test"),
                CreateUser(sellerAId, $"seller-a-{suffix}", "Seller", "A"),
                CreateUser(sellerBId, $"seller-b-{suffix}", "Seller", "B"));

            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        using var sellerAProvider = CreateProvider(sellerAId);
        using var sellerBProvider = CreateProvider(sellerBId);

        // Act
        var sellerAResponse = await SendAsync(sellerAProvider);
        var sellerBResponse = await SendAsync(sellerBProvider);

        // Assert
        Assert.That(sellerAResponse.Orders, Has.Count.EqualTo(1));
        Assert.That(sellerBResponse.Orders, Has.Count.EqualTo(1));

        var sellerAOrder = sellerAResponse.Orders.Single();
        var sellerBOrder = sellerBResponse.Orders.Single();

        Assert.That(sellerAOrder.OrderGroupId, Is.EqualTo(groupA.Id));
        Assert.That(sellerAOrder.TotalAmount, Is.EqualTo(160m));
        Assert.That(sellerAOrder.ItemCount, Is.EqualTo(1));

        Assert.That(sellerBOrder.OrderGroupId, Is.EqualTo(groupB.Id));
        Assert.That(sellerBOrder.TotalAmount, Is.EqualTo(115m));
        Assert.That(sellerBOrder.ItemCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Handle_should_return_orders_by_created_at_descending()
    {
        // Arrange
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var olderOrder = CreateOrder(buyerId, sellerId);
        var newerOrder = CreateOrder(buyerId, sellerId);

        var olderCreatedAt = new DateTime(
            2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        var newerCreatedAt = new DateTime(
            2026, 1, 2, 10, 0, 0, DateTimeKind.Utc);

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(buyerId, $"buyer-{suffix}", "Buyer", "Test"),
                CreateUser(sellerId, $"seller-{suffix}", "Seller", "Test"));

            context.Orders.AddRange(olderOrder, newerOrder);

            context.Entry(olderOrder)
                .Property(order => order.CreatedAt)
                .CurrentValue = olderCreatedAt;

            context.Entry(newerOrder)
                .Property(order => order.CreatedAt)
                .CurrentValue = newerCreatedAt;

            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(sellerId);

        // Act
        var response = await SendAsync(provider);

        // Assert
        Assert.That(response.Orders, Has.Count.EqualTo(2));
        Assert.That(
            response.Orders.Select(order => order.OrderId),
            Is.EqualTo(new[] { newerOrder.Id, olderOrder.Id }));
        Assert.That(
            response.Orders.Select(order => order.CreatedAt),
            Is.EqualTo(new[] { newerCreatedAt, olderCreatedAt }));
    }

    private static Order CreateOrder(
        Guid buyerId,
        Guid sellerId,
        decimal unitPrice = 100m,
        int quantity = 1,
        decimal discountAmount = 0m)
    {
        var order = Order.Create(
            buyerId,
            "Test Buyer",
            "09120000000",
            "Tehran",
            "Tehran",
            "1234567890",
            "Test Street");

        order.AddItem(
            sellerId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Product",
            "Default",
            "Test Seller",
            unitPrice,
            quantity,
            discountAmount);

        return order;
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
        services.AddScoped<ICurrentUser>(
            _ => new TestCurrentUser(currentUserId));

        return services.BuildServiceProvider();
    }

    private static async Task<GetSellerOrdersResponse> SendAsync(
        IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        return await sender.Send(new GetSellerOrdersQuery());
    }

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }
}
