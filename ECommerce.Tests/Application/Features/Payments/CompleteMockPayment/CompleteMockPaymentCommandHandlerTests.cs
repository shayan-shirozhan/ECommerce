using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Payments.CompleteMockPayment;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Payments.CompleteMockPayment;

public sealed class CompleteMockPaymentCommandHandlerTests : PostgresTestBase
{
    [Test]
    public async Task Handle_should_mark_payment_successful_and_confirm_order()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var seeded = await SeedPaymentAsync(userId);
        var gateway = new FixedPaymentGateway(
            new PaymentGatewayResult(true, "txn-success-001", "gateway-ref-001"));

        using var provider = CreateProvider(userId, gateway);

        // Act
        var response = await SendAsync(provider, seeded.PaymentId);

        // Verify persistence using a new DbContext.
        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(order => order.Payments)
            .SingleAsync(order => order.Id == seeded.OrderId);
        var persistedPayment = persistedOrder.Payments.Single();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.PaymentId, Is.EqualTo(seeded.PaymentId));
            Assert.That(response.OrderId, Is.EqualTo(seeded.OrderId));
            Assert.That(response.PaymentStatus, Is.EqualTo(PaymentStatus.Successful));
            Assert.That(response.OrderStatus, Is.EqualTo(OrderStatus.Confirmed));
            Assert.That(response.TransactionId, Is.EqualTo("txn-success-001"));
            Assert.That(response.GatewayReference, Is.EqualTo("gateway-ref-001"));

            Assert.That(persistedPayment.Status, Is.EqualTo(PaymentStatus.Successful));
            Assert.That(persistedPayment.TransactionId, Is.EqualTo("txn-success-001"));
            Assert.That(persistedPayment.GatewayReference, Is.EqualTo("gateway-ref-001"));
            Assert.That(persistedOrder.Status, Is.EqualTo(OrderStatus.Confirmed));

            Assert.That(gateway.CallCount, Is.EqualTo(1));
            Assert.That(gateway.LastPaymentId, Is.EqualTo(seeded.PaymentId));
            Assert.That(gateway.LastAmount, Is.EqualTo(200m));
        });
    }

    [Test]
    public async Task Handle_should_mark_payment_failed_and_cancel_order_when_gateway_declines()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var seeded = await SeedPaymentAsync(userId);
        var gateway = new FixedPaymentGateway(new PaymentGatewayResult(false, null, null));

        using var provider = CreateProvider(userId, gateway);

        // Act
        var response = await SendAsync(provider, seeded.PaymentId);

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(order => order.Payments)
            .SingleAsync(order => order.Id == seeded.OrderId);
        var persistedPayment = persistedOrder.Payments.Single();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.PaymentStatus, Is.EqualTo(PaymentStatus.Failed));
            Assert.That(response.OrderStatus, Is.EqualTo(OrderStatus.Cancelled));
            Assert.That(response.TransactionId, Is.Null);
            Assert.That(response.GatewayReference, Is.Null);
            Assert.That(persistedPayment.Status, Is.EqualTo(PaymentStatus.Failed));
            Assert.That(persistedOrder.Status, Is.EqualTo(OrderStatus.Cancelled));
            Assert.That(gateway.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Handle_should_reject_request_without_authenticated_user()
    {
        // Arrange
        var gateway = new FixedPaymentGateway(new PaymentGatewayResult(true, "txn-unused", null));
        using var provider = CreateProvider(null, gateway);

        // Act + Assert
        Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(provider, Guid.NewGuid()));

        Assert.That(gateway.CallCount, Is.Zero);
    }

    [Test]
    public void Handle_should_throw_not_found_when_payment_does_not_exist()
    {
        // Arrange
        var gateway = new FixedPaymentGateway(new PaymentGatewayResult(true, "txn-unused", null));
        using var provider = CreateProvider(Guid.NewGuid(), gateway);

        // Act + Assert
        Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(provider, Guid.NewGuid()));

        Assert.That(gateway.CallCount, Is.Zero);
    }

    [Test]
    public async Task Handle_should_not_complete_payment_for_another_users_order()
    {
        // Arrange
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var seeded = await SeedPaymentAsync(ownerUserId);
        var gateway = new FixedPaymentGateway(new PaymentGatewayResult(true, "txn-unused", null));

        using var provider = CreateProvider(otherUserId, gateway);

        // Act + Assert
        Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(provider, seeded.PaymentId));

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(order => order.Payments)
            .SingleAsync(order => order.Id == seeded.OrderId);

        Assert.Multiple(() =>
        {
            Assert.That(persistedOrder.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(persistedOrder.Payments.Single().Status, Is.EqualTo(PaymentStatus.Pending));
            Assert.That(gateway.CallCount, Is.Zero);
        });
    }

    [Test]
    public async Task Handle_should_reject_payment_that_has_already_been_processed()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var seeded = await SeedPaymentAsync(userId, markAsSuccessful: true);
        var gateway = new FixedPaymentGateway(new PaymentGatewayResult(true, "txn-second", null));
        using var provider = CreateProvider(userId, gateway);

        // Act + Assert
        Assert.ThrowsAsync<BusinessRuleException>(
            async () => await SendAsync(provider, seeded.PaymentId));

        Assert.That(gateway.CallCount, Is.Zero);
    }

    [Test]
    public async Task Handle_should_reject_payment_when_order_is_not_awaiting_payment()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var seeded = await SeedPaymentAsync(userId, cancelOrder: true);
        var gateway = new FixedPaymentGateway(new PaymentGatewayResult(true, "txn-unused", null));
        using var provider = CreateProvider(userId, gateway);

        // Act + Assert
        Assert.ThrowsAsync<BusinessRuleException>(
            async () => await SendAsync(provider, seeded.PaymentId));

        Assert.That(gateway.CallCount, Is.Zero);
    }

    [Test]
    public async Task Handle_should_not_persist_changes_when_successful_gateway_result_has_no_transaction_id()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var seeded = await SeedPaymentAsync(userId);
        var gateway = new FixedPaymentGateway(new PaymentGatewayResult(true, null, "gateway-ref"));
        using var provider = CreateProvider(userId, gateway);

        // Act + Assert
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await SendAsync(provider, seeded.PaymentId));

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(order => order.Payments)
            .SingleAsync(order => order.Id == seeded.OrderId);

        Assert.Multiple(() =>
        {
            Assert.That(persistedOrder.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(persistedOrder.Payments.Single().Status, Is.EqualTo(PaymentStatus.Pending));
            Assert.That(persistedOrder.Payments.Single().TransactionId, Is.Null);
            Assert.That(gateway.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void Validator_should_reject_empty_payment_id()
    {
        var validator = new CompleteMockPaymentCommandValidator();

        var result = validator.Validate(new CompleteMockPaymentCommand(Guid.Empty));

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public async Task Handle_should_allow_only_one_database_completion_for_concurrent_requests()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var seeded = await SeedPaymentAsync(userId);
        var gateway = new CoordinatedPaymentGateway(expectedCallCount: 2);
        using var provider = CreateProvider(userId, gateway);

        // Act: both requests load the same pending Order before either can save.
        var outcomes = await Task.WhenAll(
            CaptureFailureAsync(provider, seeded.PaymentId),
            CaptureFailureAsync(provider, seeded.PaymentId));

        // Assert: optimistic concurrency allows one commit and rejects the other.
        Assert.That(outcomes.Count(exception => exception is null), Is.EqualTo(1));
        Assert.That(outcomes.Count(exception => exception is ConcurrencyConflictException), Is.EqualTo(1));
        Assert.That(gateway.CallCount, Is.EqualTo(2));

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(order => order.Payments)
            .SingleAsync(order => order.Id == seeded.OrderId);

        Assert.Multiple(() =>
        {
            Assert.That(persistedOrder.Status, Is.EqualTo(OrderStatus.Confirmed));
            Assert.That(persistedOrder.Payments.Single().Status, Is.EqualTo(PaymentStatus.Successful));
        });
    }

    private async Task<SeededPayment> SeedPaymentAsync(
        Guid userId,
        bool markAsSuccessful = false,
        bool cancelOrder = false)
    {
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var order = CreateOrderWithItem(userId, sellerId);
        var payment = order.InitiatePayment();

        if (markAsSuccessful)
        {
            payment.MarkSuccessful($"seed-txn-{Guid.NewGuid():N}");
            order.Confirm();
        }
        else if (cancelOrder)
        {
            order.Cancel();
        }

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(userId, $"buyer-{suffix}", "Test", "Buyer"),
                CreateUser(sellerId, $"seller-{suffix}", "Test", "Seller"));

            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        return new SeededPayment(order.Id, payment.Id);
    }

    private static Order CreateOrderWithItem(Guid userId, Guid sellerId)
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

    private ServiceProvider CreateProvider(Guid? currentUserId, IPaymentGateway gateway)
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

        // Replace the production random Gateway with a deterministic test double.
        services.AddScoped<IPaymentGateway>(_ => gateway);
        services.AddScoped<ICurrentUser>(_ => new TestCurrentUser(currentUserId));

        return services.BuildServiceProvider();
    }

    private static async Task<CompleteMockPaymentResponse> SendAsync(
        IServiceProvider provider,
        Guid paymentId)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        return await sender.Send(new CompleteMockPaymentCommand(paymentId));
    }

    private static async Task<Exception?> CaptureFailureAsync(
        IServiceProvider provider,
        Guid paymentId)
    {
        try
        {
            await SendAsync(provider, paymentId);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private sealed record SeededPayment(Guid OrderId, Guid PaymentId);

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }

    private sealed class FixedPaymentGateway(PaymentGatewayResult result) : IPaymentGateway
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);
        public Guid? LastPaymentId { get; private set; }
        public decimal? LastAmount { get; private set; }

        public Task<PaymentGatewayResult> ProcessAsync(
            Guid paymentId,
            decimal amount,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _callCount);
            LastPaymentId = paymentId;
            LastAmount = amount;
            return Task.FromResult(result);
        }
    }

    private sealed class CoordinatedPaymentGateway(int expectedCallCount) : IPaymentGateway
    {
        private readonly TaskCompletionSource _allCallsReached =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public async Task<PaymentGatewayResult> ProcessAsync(
            Guid paymentId,
            decimal amount,
            CancellationToken cancellationToken = default)
        {
            var invocation = Interlocked.Increment(ref _callCount);

            if (invocation >= expectedCallCount)
                _allCallsReached.TrySetResult();

            await _allCallsReached.Task.WaitAsync(cancellationToken);

            // Different transaction IDs make the test isolate the Order xmin conflict,
            // rather than accidentally relying on the Payment.TransactionId unique index.
            return new PaymentGatewayResult(
                true,
                $"concurrent-txn-{invocation}",
                $"concurrent-ref-{invocation}");
        }
    }
}
