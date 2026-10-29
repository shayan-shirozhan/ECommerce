
using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Orders.GetMyOrderById;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Orders.GetMyOrderById;

public sealed class GetMyOrderByIdQueryHandlerTests : PostgresTestBase
{
    [Test]
    public void Handle_should_reject_request_without_authenticated_user_id()
    {
        // Arrange
        using var provider = CreateProvider(currentUserId: null);
        var orderId = Guid.NewGuid();

        // Act + Assert
        Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(provider, orderId));
    }

    [Test]
    public async Task Handle_should_throw_not_found_when_order_does_not_exist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        using var provider = CreateProvider(currentUserId: userId);

        // Act + Assert
        Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(provider, orderId));
    }

    [Test]
    public async Task Handle_should_not_return_another_users_order()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();

        var suffix = Guid.NewGuid().ToString("N");

        var currentOrder = CreateOrder(currentUserId, sellerId);
        var otherOrder = CreateOrder(otherUserId, sellerId);

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(
                    currentUserId,
                    $"buyer-a-{suffix}",
                    "Buyer",
                    "A"),
                CreateUser(
                    otherUserId,
                    $"buyer-b-{suffix}",
                    "Buyer",
                    "B"),
                CreateUser(
                    sellerId,
                    $"seller-{suffix}",
                    "Test",
                    "Seller"));

            context.Orders.AddRange(currentOrder, otherOrder);

            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(currentUserId);

        // Act + Assert
        Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(provider, otherOrder.Id));
    }

    [Test]
    public async Task Handle_should_return_order_details_with_groups_and_items()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var order = CreateOrder(
            userId,
            sellerId,
            unitPrice: 120m,
            quantity: 3,
            discountAmount: 20m);

        var expectedGroup = order.Groups.Single();
        var expectedItem = expectedGroup.Items.Single();

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(
                    userId,
                    $"buyer-{suffix}",
                    "Buyer",
                    "Test"),
                CreateUser(
                    sellerId,
                    $"seller-{suffix}",
                    "Test",
                    "Seller"));

            context.Orders.Add(order);

            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(userId);

        // Act
        var response = await SendAsync(provider, order.Id);

        // Assert: Order
        Assert.That(response.OrderId, Is.EqualTo(order.Id));
        Assert.That(response.Status, Is.EqualTo(order.Status));
        Assert.That(response.CreatedAt, Is.Not.EqualTo(default(DateTime)));
        Assert.That(response.TotalAmount, Is.EqualTo(340m));

        // Assert: Shipping information
        Assert.That(
            response.ShippingRecipientName,
            Is.EqualTo("Test Buyer"));

        Assert.That(
            response.ShippingPhoneNumber,
            Is.EqualTo("09120000000"));

        Assert.That(
            response.ShippingProvince,
            Is.EqualTo("Tehran"));

        Assert.That(
            response.ShippingCity,
            Is.EqualTo("Tehran"));

        Assert.That(
            response.ShippingPostalCode,
            Is.EqualTo("1234567890"));

        Assert.That(
            response.ShippingAddressLine,
            Is.EqualTo("Test Street"));

        // Assert: Order group
        Assert.That(response.Groups, Has.Count.EqualTo(1));

        var returnedGroup = response.Groups.Single();

        Assert.That(
            returnedGroup.OrderGroupId,
            Is.EqualTo(expectedGroup.Id));

        Assert.That(
            returnedGroup.SellerId,
            Is.EqualTo(sellerId));

        Assert.That(
            returnedGroup.Status,
            Is.EqualTo(expectedGroup.Status));

        Assert.That(
            returnedGroup.TotalAmount,
            Is.EqualTo(340m));

        // Assert: Order item
        Assert.That(returnedGroup.Items, Has.Count.EqualTo(1));

        var returnedItem = returnedGroup.Items.Single();

        Assert.That(
            returnedItem.OrderItemId,
            Is.EqualTo(expectedItem.Id));

        Assert.That(
            returnedItem.ProductId,
            Is.EqualTo(expectedItem.ProductId));

        Assert.That(
            returnedItem.ProductVariantId,
            Is.EqualTo(expectedItem.ProductVariantId));

        Assert.That(
            returnedItem.SellerOfferId,
            Is.EqualTo(expectedItem.SellerOfferId));

        Assert.That(
            returnedItem.ProductName,
            Is.EqualTo("Test Product"));

        Assert.That(
            returnedItem.VariantName,
            Is.EqualTo("Default"));

        Assert.That(
            returnedItem.SellerName,
            Is.EqualTo("Test Seller"));

        Assert.That(
            returnedItem.UnitPrice,
            Is.EqualTo(120m));

        Assert.That(
            returnedItem.Quantity,
            Is.EqualTo(3));

        Assert.That(
            returnedItem.DiscountAmount,
            Is.EqualTo(20m));

        Assert.That(
            returnedItem.TotalAmount,
            Is.EqualTo(340m));
    }

    [Test]
    public async Task Handle_should_map_all_groups_and_items()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sellerAId = Guid.NewGuid();
        var sellerBId = Guid.NewGuid();

        var suffix = Guid.NewGuid().ToString("N");

        var order = CreateOrder(userId, sellerAId);

        // A second item for seller A.
        order.AddItem(
            sellerAId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Second Product",
            "Blue",
            "Seller A",
            25m,
            1,
            5m);

        // An item for seller B, which creates a separate group.
        order.AddItem(
            sellerBId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Third Product",
            "Large",
            "Seller B",
            50m,
            4,
            10m);

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(
                    userId,
                    $"buyer-{suffix}",
                    "Buyer",
                    "Test"),
                CreateUser(
                    sellerAId,
                    $"seller-a-{suffix}",
                    "Seller",
                    "A"),
                CreateUser(
                    sellerBId,
                    $"seller-b-{suffix}",
                    "Seller",
                    "B"));

            context.Orders.Add(order);

            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(userId);

        // Act
        var response = await SendAsync(provider, order.Id);

        // Assert: Order
        Assert.That(response.OrderId, Is.EqualTo(order.Id));
        Assert.That(response.TotalAmount, Is.EqualTo(410m));
        Assert.That(response.Groups, Has.Count.EqualTo(2));

        // Assert: Both seller groups exist.
        Assert.That(
            response.Groups.Select(group => group.SellerId),
            Is.EquivalentTo(new[] { sellerAId, sellerBId }));

        var sellerAGroup = response.Groups.Single(
            group => group.SellerId == sellerAId);

        var sellerBGroup = response.Groups.Single(
            group => group.SellerId == sellerBId);

        // Seller A: 200 + 20 = 220.
        Assert.That(sellerAGroup.TotalAmount, Is.EqualTo(220m));
        Assert.That(sellerAGroup.Items, Has.Count.EqualTo(2));

        Assert.That(
            sellerAGroup.Items.Select(item => item.ProductName),
            Is.EquivalentTo(new[]
            {
                "Test Product",
                "Second Product"
            }));

        // Seller B: (50 * 4) - 10 = 190.
        Assert.That(sellerBGroup.TotalAmount, Is.EqualTo(190m));
        Assert.That(sellerBGroup.Items, Has.Count.EqualTo(1));

        var sellerBItem = sellerBGroup.Items.Single();

        Assert.That(
            sellerBItem.ProductName,
            Is.EqualTo("Third Product"));

        Assert.That(sellerBItem.UnitPrice, Is.EqualTo(50m));
        Assert.That(sellerBItem.Quantity, Is.EqualTo(4));
        Assert.That(sellerBItem.DiscountAmount, Is.EqualTo(10m));
        Assert.That(sellerBItem.TotalAmount, Is.EqualTo(190m));
    }

    private static Order CreateOrder(
        Guid userId,
        Guid sellerId,
        decimal unitPrice = 100m,
        int quantity = 2,
        decimal discountAmount = 0m)
    {
        var order = Order.Create(
            userId,
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

    private static async Task<GetMyOrderByIdResponse> SendAsync(
        IServiceProvider provider,
        Guid orderId)
    {
        using var scope = provider.CreateScope();

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        return await sender.Send(
            new GetMyOrderByIdQuery(orderId));
    }

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;

        public bool IsAuthenticated => UserId.HasValue;

        public bool IsInRole(string role) => false;
    }
}
