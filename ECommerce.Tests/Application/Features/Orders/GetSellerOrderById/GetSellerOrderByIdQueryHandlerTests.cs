using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Orders.GetSellerOrderById;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Orders.GetSellerOrderById;

public sealed class GetSellerOrderByIdQueryHandlerTests : PostgresTestBase
{
    [Test]
    public void Handle_should_reject_request_without_authenticated_user_id()
    {
        // Arrange
        using var provider = CreateProvider(currentUserId: null);

        // Act + Assert
        Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(provider, Guid.NewGuid()));
    }

    [Test]
    public async Task Handle_should_throw_not_found_when_order_does_not_exist()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        using var provider = CreateProvider(sellerId);

        // Act + Assert
        Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(provider, orderId));
    }

    [Test]
    public async Task Handle_should_not_return_order_when_current_seller_does_not_own_a_group()
    {
        // Arrange
        var buyerId = Guid.NewGuid();
        var orderSellerId = Guid.NewGuid();
        var currentSellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var order = CreateOrder(buyerId, orderSellerId);

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(buyerId, $"buyer-{suffix}", "Buyer", "Test"),
                CreateUser(orderSellerId, $"order-seller-{suffix}", "Order", "Seller"),
                CreateUser(currentSellerId, $"current-seller-{suffix}", "Current", "Seller"));

            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(currentSellerId);

        // Act + Assert
        Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(provider, order.Id));
    }

    [Test]
    public async Task Handle_should_return_only_current_sellers_group_and_map_its_details()
    {
        // Arrange
        var buyerId = Guid.NewGuid();
        var currentSellerId = Guid.NewGuid();
        var otherSellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var order = CreateOrder(
            buyerId,
            currentSellerId,
            unitPrice: 120m,
            quantity: 3,
            discountAmount: 20m);

        // A second item belongs to the current seller and remains in the same group.
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

        // This item creates a separate group and must not be returned to the current seller.
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

        var expectedCurrentSellerGroup = order.Groups.Single(
            group => group.SellerId == currentSellerId);
        var otherSellerGroup = order.Groups.Single(
            group => group.SellerId == otherSellerId);

        var expectedFirstItem = expectedCurrentSellerGroup.Items.Single(
            item => item.ProductName == "Test Product");
        var expectedSecondItem = expectedCurrentSellerGroup.Items.Single(
            item => item.ProductName == "Second Current Seller Product");

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(buyerId, $"buyer-{suffix}", "Buyer", "Test"),
                CreateUser(currentSellerId, $"current-seller-{suffix}", "Current", "Seller"),
                CreateUser(otherSellerId, $"other-seller-{suffix}", "Other", "Seller"));

            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(currentSellerId);

        // Act
        var response = await SendAsync(provider, order.Id);

        // Assert: order details
        Assert.That(response.OrderId, Is.EqualTo(order.Id));
        Assert.That(response.OrderStatus, Is.EqualTo(order.Status));
        Assert.That(response.CreatedAt, Is.Not.EqualTo(default(DateTime)));

        // Assert: shipping information
        Assert.That(response.ShippingRecipientName, Is.EqualTo("Test Buyer"));
        Assert.That(response.ShippingPhoneNumber, Is.EqualTo("09120000000"));
        Assert.That(response.ShippingProvince, Is.EqualTo("Tehran"));
        Assert.That(response.ShippingCity, Is.EqualTo("Tehran"));
        Assert.That(response.ShippingPostalCode, Is.EqualTo("1234567890"));
        Assert.That(response.ShippingAddressLine, Is.EqualTo("Test Street"));

        // Assert: only the current seller's group is returned.
        Assert.That(response.OrderGroupId, Is.EqualTo(expectedCurrentSellerGroup.Id));
        Assert.That(response.OrderGroupId, Is.Not.EqualTo(otherSellerGroup.Id));
        Assert.That(response.OrderGroupStatus, Is.EqualTo(expectedCurrentSellerGroup.Status));
        Assert.That(response.TotalAmount, Is.EqualTo(430m));
        Assert.That(response.TotalAmount, Is.Not.EqualTo(order.TotalAmount));

        // Assert: every item in the current seller's group is mapped.
        Assert.That(response.Items, Has.Count.EqualTo(2));
        Assert.That(
            response.Items.Select(item => item.ProductName),
            Is.EquivalentTo(new[] { "Test Product", "Second Current Seller Product" }));
        Assert.That(
            response.Items.Any(item => item.ProductName == "Other Seller Product"),
            Is.False);

        var returnedFirstItem = response.Items.Single(
            item => item.ProductName == expectedFirstItem.ProductName);

        Assert.That(returnedFirstItem.OrderItemId, Is.EqualTo(expectedFirstItem.Id));
        Assert.That(returnedFirstItem.ProductId, Is.EqualTo(expectedFirstItem.ProductId));
        Assert.That(returnedFirstItem.ProductVariantId, Is.EqualTo(expectedFirstItem.ProductVariantId));
        Assert.That(returnedFirstItem.SellerOfferId, Is.EqualTo(expectedFirstItem.SellerOfferId));
        Assert.That(returnedFirstItem.VariantName, Is.EqualTo(expectedFirstItem.VariantName));
        Assert.That(returnedFirstItem.SellerName, Is.EqualTo(expectedFirstItem.SellerName));
        Assert.That(returnedFirstItem.UnitPrice, Is.EqualTo(120m));
        Assert.That(returnedFirstItem.Quantity, Is.EqualTo(3));
        Assert.That(returnedFirstItem.DiscountAmount, Is.EqualTo(20m));
        Assert.That(returnedFirstItem.TotalAmount, Is.EqualTo(340m));

        var returnedSecondItem = response.Items.Single(
            item => item.ProductName == expectedSecondItem.ProductName);

        Assert.That(returnedSecondItem.OrderItemId, Is.EqualTo(expectedSecondItem.Id));
        Assert.That(returnedSecondItem.UnitPrice, Is.EqualTo(50m));
        Assert.That(returnedSecondItem.Quantity, Is.EqualTo(2));
        Assert.That(returnedSecondItem.DiscountAmount, Is.EqualTo(10m));
        Assert.That(returnedSecondItem.TotalAmount, Is.EqualTo(90m));
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

    private static async Task<GetSellerOrderByIdResponse> SendAsync(
        IServiceProvider provider,
        Guid orderId)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        return await sender.Send(new GetSellerOrderByIdQuery(orderId));
    }

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }
}
