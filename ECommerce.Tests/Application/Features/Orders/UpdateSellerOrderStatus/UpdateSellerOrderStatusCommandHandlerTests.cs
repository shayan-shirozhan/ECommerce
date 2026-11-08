using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Orders.UpdateSellerOrderStatus;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Orders.UpdateSellerOrderStatus;

public sealed class UpdateSellerOrderStatusCommandHandlerTests : PostgresTestBase
{
    [Test]
    public async Task Handle_should_move_current_seller_group_to_processing_and_persist_changes()
    {
        // Arrange
        var seeded = await SeedConfirmedOrderAsync();
        using var provider = CreateProvider(seeded.FirstSellerId);

        // Act
        var response = await SendAsync(
            provider,
            seeded.OrderId,
            OrderGroupStatus.Processing);

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(order => order.Groups)
            .SingleAsync(order => order.Id == seeded.OrderId);
        var firstGroup = GetSellerGroup(persistedOrder, seeded.FirstSellerId);
        var secondGroup = GetSellerGroup(persistedOrder, seeded.SecondSellerId);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.OrderId, Is.EqualTo(seeded.OrderId));
            Assert.That(response.OrderStatus, Is.EqualTo(OrderStatus.Processing));
            Assert.That(response.OrderGroupId, Is.EqualTo(firstGroup.Id));
            Assert.That(response.OrderGroupStatus, Is.EqualTo(OrderGroupStatus.Processing));

            Assert.That(persistedOrder.Status, Is.EqualTo(OrderStatus.Processing));
            Assert.That(firstGroup.Status, Is.EqualTo(OrderGroupStatus.Processing));
            Assert.That(secondGroup.Status, Is.EqualTo(OrderGroupStatus.Pending));
        });
    }

    [Test]
    public async Task Handle_should_allow_second_seller_to_start_processing_after_first_seller()
    {
        // Arrange: the first seller has already started processing.
        var seeded = await SeedConfirmedOrderAsync(
            (order, firstSellerId, _) => order.MoveSellerGroupToProcessing(firstSellerId));
        using var provider = CreateProvider(seeded.SecondSellerId);

        // Act
        var response = await SendAsync(
            provider,
            seeded.OrderId,
            OrderGroupStatus.Processing);

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(order => order.Groups)
            .SingleAsync(order => order.Id == seeded.OrderId);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.OrderStatus, Is.EqualTo(OrderStatus.Processing));
            Assert.That(response.OrderGroupId,
                Is.EqualTo(GetSellerGroup(persistedOrder, seeded.SecondSellerId).Id));
            Assert.That(GetSellerGroup(persistedOrder, seeded.FirstSellerId).Status,
                Is.EqualTo(OrderGroupStatus.Processing));
            Assert.That(GetSellerGroup(persistedOrder, seeded.SecondSellerId).Status,
                Is.EqualTo(OrderGroupStatus.Processing));
            Assert.That(persistedOrder.Status, Is.EqualTo(OrderStatus.Processing));
        });
    }

    [Test]
    public async Task Handle_should_keep_order_processing_until_all_seller_groups_are_shipped()
    {
        // Arrange: both groups are Processing.
        var seeded = await SeedConfirmedOrderAsync((order, firstSellerId, secondSellerId) =>
        {
            order.MoveSellerGroupToProcessing(firstSellerId);
            order.MoveSellerGroupToProcessing(secondSellerId);
        });
        using var provider = CreateProvider(seeded.FirstSellerId);

        // Act: only the first seller ships their group.
        var response = await SendAsync(
            provider,
            seeded.OrderId,
            OrderGroupStatus.Shipped);

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(order => order.Groups)
            .SingleAsync(order => order.Id == seeded.OrderId);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.OrderStatus, Is.EqualTo(OrderStatus.Processing));
            Assert.That(response.OrderGroupStatus, Is.EqualTo(OrderGroupStatus.Shipped));
            Assert.That(persistedOrder.Status, Is.EqualTo(OrderStatus.Processing));
            Assert.That(GetSellerGroup(persistedOrder, seeded.FirstSellerId).Status,
                Is.EqualTo(OrderGroupStatus.Shipped));
            Assert.That(GetSellerGroup(persistedOrder, seeded.SecondSellerId).Status,
                Is.EqualTo(OrderGroupStatus.Processing));
        });

        // Act: the second seller ships their group; now every group is shipped.
        using var secondSellerProvider = CreateProvider(seeded.SecondSellerId);
        var secondResponse = await SendAsync(
            secondSellerProvider,
            seeded.OrderId,
            OrderGroupStatus.Shipped);

        await using var finalContext = CreateDbContext();
        var finalOrder = await finalContext.Orders
            .Include(order => order.Groups)
            .SingleAsync(order => order.Id == seeded.OrderId);

        Assert.Multiple(() =>
        {
            Assert.That(secondResponse.OrderStatus, Is.EqualTo(OrderStatus.Shipped));
            Assert.That(finalOrder.Status, Is.EqualTo(OrderStatus.Shipped));
            Assert.That(finalOrder.Groups.Select(group => group.Status),
                Is.All.EqualTo(OrderGroupStatus.Shipped));
        });
    }

    [Test]
    public async Task Handle_should_mark_order_delivered_only_after_all_seller_groups_are_delivered()
    {
        // Arrange: both groups are Shipped.
        var seeded = await SeedConfirmedOrderAsync((order, firstSellerId, secondSellerId) =>
        {
            order.MoveSellerGroupToProcessing(firstSellerId);
            order.MoveSellerGroupToProcessing(secondSellerId);
            order.MoveSellerGroupToShipped(firstSellerId);
            order.MoveSellerGroupToShipped(secondSellerId);
        });

        using var firstSellerProvider = CreateProvider(seeded.FirstSellerId);
        using var secondSellerProvider = CreateProvider(seeded.SecondSellerId);

        // Act: the first seller delivers their group.
        var firstResponse = await SendAsync(
            firstSellerProvider,
            seeded.OrderId,
            OrderGroupStatus.Delivered);

        // The aggregate must remain Shipped while the second group is still Shipped.
        await using (var intermediateContext = CreateDbContext())
        {
            var intermediateOrder = await intermediateContext.Orders
                .Include(order => order.Groups)
                .SingleAsync(order => order.Id == seeded.OrderId);

            Assert.Multiple(() =>
            {
                Assert.That(firstResponse.OrderStatus, Is.EqualTo(OrderStatus.Shipped));
                Assert.That(firstResponse.OrderGroupStatus, Is.EqualTo(OrderGroupStatus.Delivered));
                Assert.That(intermediateOrder.Status, Is.EqualTo(OrderStatus.Shipped));
                Assert.That(GetSellerGroup(intermediateOrder, seeded.SecondSellerId).Status,
                    Is.EqualTo(OrderGroupStatus.Shipped));
            });
        }

        // Act: the second seller delivers their group.
        var secondResponse = await SendAsync(
            secondSellerProvider,
            seeded.OrderId,
            OrderGroupStatus.Delivered);

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(order => order.Groups)
            .SingleAsync(order => order.Id == seeded.OrderId);

        // Assert: the aggregate becomes Delivered only after the final group is delivered.
        Assert.Multiple(() =>
        {
            Assert.That(secondResponse.OrderStatus, Is.EqualTo(OrderStatus.Delivered));
            Assert.That(secondResponse.OrderGroupStatus, Is.EqualTo(OrderGroupStatus.Delivered));
            Assert.That(persistedOrder.Status, Is.EqualTo(OrderStatus.Delivered));
            Assert.That(persistedOrder.Groups.Select(group => group.Status),
                Is.All.EqualTo(OrderGroupStatus.Delivered));
        });
    }

    [Test]
    public void Handle_should_reject_request_without_authenticated_user_id()
    {
        // Arrange
        using var provider = CreateProvider(currentUserId: null);

        // Act + Assert
        Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(
                provider,
                Guid.NewGuid(),
                OrderGroupStatus.Processing));
    }

    [Test]
    public void Handle_should_throw_not_found_when_order_does_not_exist()
    {
        // Arrange
        using var provider = CreateProvider(Guid.NewGuid());

        // Act + Assert
        Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(
                provider,
                Guid.NewGuid(),
                OrderGroupStatus.Processing));
    }

    [Test]
    public async Task Handle_should_not_expose_or_update_order_owned_by_other_sellers()
    {
        // Arrange
        var seeded = await SeedConfirmedOrderAsync();
        var unrelatedSellerId = Guid.NewGuid();
        using var provider = CreateProvider(unrelatedSellerId);

        // Act + Assert
        Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(
                provider,
                seeded.OrderId,
                OrderGroupStatus.Processing));

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(order => order.Groups)
            .SingleAsync(order => order.Id == seeded.OrderId);

        Assert.Multiple(() =>
        {
            Assert.That(persistedOrder.Status, Is.EqualTo(OrderStatus.Confirmed));
            Assert.That(persistedOrder.Groups.Select(group => group.Status),
                Is.All.EqualTo(OrderGroupStatus.Pending));
        });
    }

    [Test]
    public async Task Handle_should_reject_domain_transition_that_is_not_allowed_without_persisting_changes()
    {
        // Arrange: a Confirmed order cannot move directly to Delivered.
        var seeded = await SeedConfirmedOrderAsync();
        using var provider = CreateProvider(seeded.FirstSellerId);

        // Act + Assert
        Assert.ThrowsAsync<DomainException>(
            async () => await SendAsync(
                provider,
                seeded.OrderId,
                OrderGroupStatus.Delivered));

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(order => order.Groups)
            .SingleAsync(order => order.Id == seeded.OrderId);

        Assert.Multiple(() =>
        {
            Assert.That(persistedOrder.Status, Is.EqualTo(OrderStatus.Confirmed));
            Assert.That(persistedOrder.Groups.Select(group => group.Status),
                Is.All.EqualTo(OrderGroupStatus.Pending));
        });
    }

    [Test]
    public void Handle_should_reject_pending_as_a_requested_status()
    {
        // Arrange
        using var provider = CreateProvider(Guid.NewGuid());

        // Act + Assert: the Application validator rejects this before the handler runs.
        Assert.ThrowsAsync<ValidationException>(
            async () => await SendAsync(
                provider,
                Guid.NewGuid(),
                OrderGroupStatus.Pending));
    }

    private async Task<SeededOrder> SeedConfirmedOrderAsync(
        Action<Order, Guid, Guid>? configureOrder = null)
    {
        var buyerId = Guid.NewGuid();
        var firstSellerId = Guid.NewGuid();
        var secondSellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var order = Order.Create(
            buyerId,
            "Test Buyer",
            "09120000000",
            "Tehran",
            "Tehran",
            "1234567890",
            "Test Street");

        order.AddItem(
            firstSellerId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "First Seller Product",
            "Default",
            "First Seller",
            100m,
            1,
            0m);

        order.AddItem(
            secondSellerId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Second Seller Product",
            "Default",
            "Second Seller",
            150m,
            1,
            0m);

        var payment = order.InitiatePayment();
        payment.MarkSuccessful($"seed-txn-{Guid.NewGuid():N}");
        order.Confirm();

        configureOrder?.Invoke(order, firstSellerId, secondSellerId);

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(buyerId, $"buyer-{suffix}", "Test", "Buyer"),
                CreateUser(firstSellerId, $"seller-a-{suffix}", "Seller", "A"),
                CreateUser(secondSellerId, $"seller-b-{suffix}", "Seller", "B"));

            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        return new SeededOrder(order.Id, firstSellerId, secondSellerId);
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

    private static async Task<UpdateSellerOrderStatusResponse> SendAsync(
        IServiceProvider provider,
        Guid orderId,
        OrderGroupStatus newStatus)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        return await sender.Send(new UpdateSellerOrderStatusCommand(orderId, newStatus));
    }

    private static OrderGroup GetSellerGroup(Order order, Guid sellerId)
    {
        return order.Groups.Single(group => group.SellerId == sellerId);
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

    private sealed record SeededOrder(
        Guid OrderId,
        Guid FirstSellerId,
        Guid SecondSellerId);

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }
}
