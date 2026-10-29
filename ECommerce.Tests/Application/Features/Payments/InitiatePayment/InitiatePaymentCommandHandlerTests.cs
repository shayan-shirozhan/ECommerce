
using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Payments.InitiatePayment;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Payments.InitiatePayment;

public sealed class InitiatePaymentCommandHandlerTests : PostgresTestBase
{
    [Test]
    public async Task Handle_should_create_payment_and_persist_order_status()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var order = CreateOrderWithItem(userId, sellerId);

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(
                    userId,
                    $"buyer-{suffix}",
                    "Test",
                    "Buyer"),
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

        // Verify using a new DbContext.
        await using var verificationContext = CreateDbContext();

        var persistedOrder = await verificationContext.Orders
            .Include(x => x.Payments)
            .SingleAsync(x => x.Id == order.Id);

        var persistedPayment = persistedOrder.Payments.Single();

        // Assert: Response
        var (
            responsePaymentId,
            responseOrderId,
            responseAmount,
            responseMethod,
            responseStatus) = response;

        Assert.Multiple(() =>
        {
            Assert.That(responsePaymentId, Is.EqualTo(persistedPayment.Id));
            Assert.That(responseOrderId, Is.EqualTo(order.Id));
            Assert.That(responseAmount, Is.EqualTo(200m));
            Assert.That(responseMethod, Is.EqualTo(PaymentMethod.Mock));
            Assert.That(responseStatus, Is.EqualTo(PaymentStatus.Pending));
        });

        // Assert: Persisted order
        Assert.Multiple(() =>
        {
            Assert.That(
                persistedOrder.Status,
                Is.EqualTo(OrderStatus.AwaitingPayment));

            Assert.That(persistedOrder.TotalAmount, Is.EqualTo(200m));
            Assert.That(persistedOrder.Payments, Has.Count.EqualTo(1));
        });

        // Assert: Persisted payment
        Assert.Multiple(() =>
        {
            Assert.That(persistedPayment.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(persistedPayment.OrderId, Is.EqualTo(order.Id));
            Assert.That(persistedPayment.Amount, Is.EqualTo(200m));

            Assert.That(
                persistedPayment.PaymentMethod,
                Is.EqualTo(PaymentMethod.Mock));

            Assert.That(
                persistedPayment.Status,
                Is.EqualTo(PaymentStatus.Pending));
        });
    }

    [Test]
    public void Handle_should_reject_request_without_authenticated_user()
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
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        int paymentCountBefore;

        await using (var context = CreateDbContext())
        {
            paymentCountBefore = await context.Payments.CountAsync();
        }

        using var provider = CreateProvider(userId);

        Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(provider, orderId));

        await using var verificationContext = CreateDbContext();

        var paymentCountAfter =
            await verificationContext.Payments.CountAsync();

        Assert.That(paymentCountAfter, Is.EqualTo(paymentCountBefore));
    }

    [Test]
    public async Task Handle_should_not_initiate_payment_for_another_users_order()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var ownerUserId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var order = CreateOrderWithItem(ownerUserId, sellerId);

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(
                    currentUserId,
                    $"buyer-a-{suffix}",
                    "Buyer",
                    "A"),
                CreateUser(
                    ownerUserId,
                    $"buyer-b-{suffix}",
                    "Buyer",
                    "B"),
                CreateUser(
                    sellerId,
                    $"seller-{suffix}",
                    "Test",
                    "Seller"));

            context.Orders.Add(order);

            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(currentUserId);

        // Act + Assert
        Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(provider, order.Id));

        // Verify that the other user's order remains unchanged.
        await using var verificationContext = CreateDbContext();

        var persistedOrder = await verificationContext.Orders
            .Include(x => x.Payments)
            .SingleAsync(x => x.Id == order.Id);

        Assert.Multiple(() =>
        {
            Assert.That(
                persistedOrder.Status,
                Is.EqualTo(OrderStatus.Pending));

            Assert.That(persistedOrder.Payments, Is.Empty);
        });
    }

    [Test]
    public async Task Handle_should_reject_order_that_is_already_awaiting_payment()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var order = CreateOrderWithItem(userId, sellerId);

        // Create the existing payment through the domain behavior.
        var existingPayment = order.InitiatePayment();

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(
                    userId,
                    $"buyer-{suffix}",
                    "Test",
                    "Buyer"),
                CreateUser(
                    sellerId,
                    $"seller-{suffix}",
                    "Test",
                    "Seller"));

            context.Orders.Add(order);

            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(userId);

        // Act + Assert
        Assert.ThrowsAsync<DomainException>(
            async () => await SendAsync(provider, order.Id));

        // Verify the failed request did not create a second payment.
        await using var verificationContext = CreateDbContext();

        var persistedOrder = await verificationContext.Orders
            .Include(x => x.Payments)
            .SingleAsync(x => x.Id == order.Id);

        Assert.Multiple(() =>
        {
            Assert.That(
                persistedOrder.Status,
                Is.EqualTo(OrderStatus.AwaitingPayment));

            Assert.That(persistedOrder.Payments, Has.Count.EqualTo(1));

            Assert.That(
                persistedOrder.Payments.Single().Id,
                Is.EqualTo(existingPayment.Id));
        });
    }

    [Test]
    public async Task Handle_should_reject_zero_amount_and_preserve_order()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var order = CreateEmptyOrder(userId);

        await using (var context = CreateDbContext())
        {
            context.Users.Add(
                CreateUser(
                    userId,
                    $"buyer-{suffix}",
                    "Test",
                    "Buyer"));

            context.Orders.Add(order);

            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(userId);

        // Act + Assert
        Assert.ThrowsAsync<DomainException>(
            async () => await SendAsync(provider, order.Id));

        // Verify that the invalid payment was not persisted.
        await using var verificationContext = CreateDbContext();

        var persistedOrder = await verificationContext.Orders
            .Include(x => x.Payments)
            .SingleAsync(x => x.Id == order.Id);

        Assert.Multiple(() =>
        {
            Assert.That(persistedOrder.TotalAmount, Is.Zero);

            Assert.That(
                persistedOrder.Status,
                Is.EqualTo(OrderStatus.Pending));

            Assert.That(persistedOrder.Payments, Is.Empty);
        });
    }

    private static Order CreateOrderWithItem(
        Guid userId,
        Guid sellerId)
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
            200m,
            1,
            0m);

        return order;
    }

    private static Order CreateEmptyOrder(Guid userId)
    {
        return Order.Create(
            userId,
            "Test Buyer",
            "09120000000",
            "Tehran",
            "Tehran",
            "1234567890",
            "Test Street");
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

    private static async Task<InitiatePaymentResponse> SendAsync(
        IServiceProvider provider,
        Guid orderId)
    {
        using var scope = provider.CreateScope();

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        return await sender.Send(
            new InitiatePaymentCommand(orderId));
    }

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;

        public bool IsAuthenticated => UserId.HasValue;

        public bool IsInRole(string role) => false;
    }
}
