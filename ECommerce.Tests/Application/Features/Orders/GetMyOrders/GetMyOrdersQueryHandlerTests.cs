
using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Orders.GetMyOrders;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Orders.GetMyOrders;

public sealed class GetMyOrdersQueryHandlerTests : PostgresTestBase
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
    public async Task Handle_should_return_empty_orders_when_user_has_no_orders()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var provider = CreateProvider(currentUserId: userId);

        // Act
        var response = await SendAsync(provider);

        // Assert
        Assert.That(response.Orders, Is.Empty);
    }

    [Test]
    public async Task Handle_should_return_only_current_users_orders()
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

        // Act
        var response = await SendAsync(provider);

        // Assert
        Assert.That(response.Orders, Has.Count.EqualTo(1));

        var returnedOrder = response.Orders.Single();

        Assert.That(returnedOrder.OrderId, Is.EqualTo(currentOrder.Id));
        Assert.That(returnedOrder.TotalAmount, Is.EqualTo(200m));
        Assert.That(returnedOrder.OrderItemCount, Is.EqualTo(1));
    }


    [Test]
    public async Task Handle_should_return_orders_by_created_at_descending()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var olderOrder = CreateOrder(userId, sellerId);
        var newerOrder = CreateOrder(userId, sellerId);

        var olderCreatedAt = new DateTime(
            2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        var newerCreatedAt = new DateTime(
            2026, 1, 2, 10, 0, 0, DateTimeKind.Utc);

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(
                    userId, $"buyer-{suffix}", "Buyer", "Test"),
                CreateUser(
                    sellerId, $"seller-{suffix}", "Test", "Seller"));

            context.Orders.AddRange(olderOrder, newerOrder);

            context.Entry(olderOrder)
                .Property(order => order.CreatedAt)
                .CurrentValue = olderCreatedAt;

            context.Entry(newerOrder)
                .Property(order => order.CreatedAt)
                .CurrentValue = newerCreatedAt;

            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(userId);

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

    [Test]
    public async Task Handle_should_count_order_item_rows_across_all_groups()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sellerAId = Guid.NewGuid();
        var sellerBId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        // CreateOrder already adds one item for seller A.
        var order = CreateOrder(userId, sellerAId);

        // Add a second item to seller A's group.
        order.AddItem(
            sellerAId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Second Product",
            "Default",
            "Seller A",
            25m,
            1,
            0m);

        // Add an item for seller B, creating a second group.
        order.AddItem(
            sellerBId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Third Product",
            "Default",
            "Seller B",
            50m,
            4,
            0m);

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(
                    userId, $"buyer-{suffix}", "Buyer", "Test"),
                CreateUser(
                    sellerAId, $"seller-a-{suffix}", "Seller", "A"),
                CreateUser(
                    sellerBId, $"seller-b-{suffix}", "Seller", "B"));

            context.Orders.Add(order);

            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(userId);

        // Act
        var response = await SendAsync(provider);

        // Assert
        Assert.That(response.Orders, Has.Count.EqualTo(1));

        var returnedOrder = response.Orders.Single();

        Assert.That(returnedOrder.OrderItemCount, Is.EqualTo(3));
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

    private static async Task<GetMyOrdersResponse> SendAsync(
        IServiceProvider provider)
    {
        using var scope = provider.CreateScope();

        var sender = scope.ServiceProvider
            .GetRequiredService<ISender>();

        return await sender.Send(new GetMyOrdersQuery());
    }

    private static Order CreateOrder(Guid userId, Guid sellerId)
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
            100m,
            2,
            0m);

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

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;

        public bool IsAuthenticated => UserId.HasValue;

        public bool IsInRole(string role) => false;
    }
}
