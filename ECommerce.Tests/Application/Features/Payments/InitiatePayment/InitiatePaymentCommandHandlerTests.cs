using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Payments.InitiatePayment;
using ECommerce.Application.Options;
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
        var responsePaymentId = response.PaymentId;
        var responseOrderId = response.OrderId;
        var responseAmount = response.Amount;
        var responseMethod = response.PaymentMethod;
        var responseStatus = response.Status;

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
    public async Task Handle_should_reject_order_that_has_an_active_payment()
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

        // The application handler rejects an active attempt before invoking the domain method.
        Assert.ThrowsAsync<BusinessRuleException>(
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
    public async Task Handle_should_create_a_new_payment_after_previous_attempt_failed()
    {
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var order = CreateOrderWithItem(userId, sellerId);
        var firstPayment = order.InitiatePayment();
        firstPayment.MarkFailed();

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(userId, $"buyer-{suffix}", "Test", "Buyer"),
                CreateUser(sellerId, $"seller-{suffix}", "Test", "Seller"));
            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(userId);

        var response = await SendAsync(provider, order.Id);

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == order.Id);

        Assert.Multiple(() =>
        {
            Assert.That(persistedOrder.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(persistedOrder.Payments, Has.Count.EqualTo(2));
            Assert.That(persistedOrder.Payments.Single(payment => payment.Id == firstPayment.Id).Status,
                Is.EqualTo(PaymentStatus.Failed));
            Assert.That(persistedOrder.Payments.Single(payment => payment.Id == response.PaymentId).Status,
                Is.EqualTo(PaymentStatus.Pending));
            Assert.That(response.PaymentId, Is.Not.EqualTo(firstPayment.Id));
            Assert.That(response.OrderId, Is.EqualTo(order.Id));
        });
    }

    [Test]
    public async Task Handle_should_reject_retry_when_order_payment_window_has_expired()
    {
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var order = CreateOrderWithItem(userId, sellerId);

        var previousPayment = order.InitiatePayment();
        previousPayment.MarkFailed();

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(userId, $"buyer-{suffix}", "Test", "Buyer"),
                CreateUser(sellerId, $"seller-{suffix}", "Test", "Seller"));

            context.Orders.Add(order);

            context.Entry(order).Property(x => x.CreatedAt).CurrentValue =
                DateTime.UtcNow.AddMinutes(-31);

            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(userId);

        Assert.ThrowsAsync<BusinessRuleException>(
            async () => await SendAsync(provider, order.Id));

        await using var verificationContext = CreateDbContext();

        var persistedOrder = await verificationContext.Orders
            .Include(x => x.Payments)
            .SingleAsync(x => x.Id == order.Id);

        Assert.Multiple(() =>
        {
            Assert.That(persistedOrder.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(persistedOrder.Payments, Has.Count.EqualTo(1));
            Assert.That(
                persistedOrder.Payments.Single().Status,
                Is.EqualTo(PaymentStatus.Failed));
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

    [Test]
    public async Task Handle_should_request_zarinpal_payment_and_persist_authority()
    {
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var authority = $"A{Guid.NewGuid():N}";
        var gateway = new FixedZarinPalGateway(
            new PaymentGatewayRequestResult(
                true,
                authority,
                "https://www.zarinpal.com/pg/StartPay/" + authority));
        var order = CreateOrderWithItem(userId, sellerId);

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(userId, $"buyer-{suffix}", "Test", "Buyer"),
                CreateUser(sellerId, $"seller-{suffix}", "Test", "Seller"));
            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(userId, gateway);
        var response = await SendAsync(
            provider,
            order.Id,
            PaymentMethod.ZarinPal);

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == order.Id);
        var persistedPayment = persistedOrder.Payments.Single();

        Assert.Multiple(() =>
        {
            Assert.That(response.PaymentMethod, Is.EqualTo(PaymentMethod.ZarinPal));
            Assert.That(response.Status, Is.EqualTo(PaymentStatus.Pending));
            Assert.That(response.RedirectUrl, Does.Contain(authority));
            Assert.That(persistedPayment.Authority, Is.EqualTo(authority));
            Assert.That(persistedPayment.Status, Is.EqualTo(PaymentStatus.Pending));
            Assert.That(gateway.RequestCallCount, Is.EqualTo(1));
            Assert.That(gateway.LastPaymentId, Is.EqualTo(persistedPayment.Id));
            Assert.That(gateway.LastOrderId, Is.EqualTo(order.Id));
            Assert.That(gateway.LastAmount, Is.EqualTo(200m));
        });
    }

    [Test]
    public async Task Handle_should_return_existing_checkout_url_when_initiation_is_retried()
    {
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var authority = $"A{Guid.NewGuid():N}";
        var gateway = new FixedZarinPalGateway(
            new PaymentGatewayRequestResult(
                true,
                authority,
                "https://www.zarinpal.com/pg/StartPay/" + authority));
        var order = CreateOrderWithItem(userId, sellerId);

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(userId, $"buyer-{suffix}", "Test", "Buyer"),
                CreateUser(sellerId, $"seller-{suffix}", "Test", "Seller"));
            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(userId, gateway);
        var firstResponse = await SendAsync(provider, order.Id, PaymentMethod.ZarinPal);
        var retryResponse = await SendAsync(provider, order.Id, PaymentMethod.ZarinPal);

        Assert.Multiple(() =>
        {
            Assert.That(retryResponse.PaymentId, Is.EqualTo(firstResponse.PaymentId));
            Assert.That(retryResponse.RedirectUrl, Is.EqualTo(firstResponse.RedirectUrl));
            Assert.That(gateway.RequestCallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Handle_should_mark_payment_failed_when_zarinpal_rejects_request()
    {
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var gateway = new FixedZarinPalGateway(
            new PaymentGatewayRequestResult(false, null, null, "-9", "Invalid amount"));
        var order = CreateOrderWithItem(userId, sellerId);

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(userId, $"buyer-{suffix}", "Test", "Buyer"),
                CreateUser(sellerId, $"seller-{suffix}", "Test", "Seller"));
            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(userId, gateway);

        Assert.ThrowsAsync<PaymentGatewayException>(
            async () => await SendAsync(provider, order.Id, PaymentMethod.ZarinPal));

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == order.Id);

        Assert.Multiple(() =>
        {
            Assert.That(persistedOrder.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(persistedOrder.Payments, Has.Count.EqualTo(1));
            Assert.That(persistedOrder.Payments.Single().Status, Is.EqualTo(PaymentStatus.Failed));
            Assert.That(gateway.RequestCallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Handle_should_keep_payment_pending_when_gateway_returns_indeterminate_request_result()
    {
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var order = CreateOrderWithItem(userId, sellerId);
        var gateway = new FixedZarinPalGateway(
            new PaymentGatewayRequestResult(
                false,
                null,
                null,
                "-54",
                "Unknown provider result",
                IsIndeterminate: true));

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(userId, $"buyer-{suffix}", "Test", "Buyer"),
                CreateUser(sellerId, $"seller-{suffix}", "Test", "Seller"));
            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(userId, gateway);

        Assert.ThrowsAsync<PaymentGatewayException>(
            async () => await SendAsync(provider, order.Id, PaymentMethod.ZarinPal));

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == order.Id);

        Assert.Multiple(() =>
        {
            Assert.That(persistedOrder.Payments, Has.Count.EqualTo(1));
            Assert.That(persistedOrder.Payments.Single().Status, Is.EqualTo(PaymentStatus.Pending));
            Assert.That(persistedOrder.Payments.Single().Authority, Is.Null);
            Assert.That(persistedOrder.Payments.Single().LastGatewayRequestStartedAt, Is.Not.Null);
            Assert.That(gateway.RequestCallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Handle_should_keep_payment_pending_when_gateway_request_outcome_is_unknown()
    {
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var order = CreateOrderWithItem(userId, sellerId);
        var gateway = new ThrowingZarinPalGateway(
            new PaymentGatewayException("Provider request timed out."));

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(userId, $"buyer-{suffix}", "Test", "Buyer"),
                CreateUser(sellerId, $"seller-{suffix}", "Test", "Seller"));
            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(userId, gateway);

        Assert.ThrowsAsync<PaymentGatewayException>(
            async () => await SendAsync(provider, order.Id, PaymentMethod.ZarinPal));

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == order.Id);
        var payment = persistedOrder.Payments.Single();

        Assert.Multiple(() =>
        {
            Assert.That(persistedOrder.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Pending));
            Assert.That(payment.Authority, Is.Null);
            Assert.That(payment.LastGatewayRequestStartedAt, Is.Not.Null);
            Assert.That(gateway.RequestCallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Handle_should_block_retry_while_gateway_request_lease_is_active()
    {
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var order = CreateOrderWithItem(userId, sellerId);
        var payment = order.InitiatePayment(PaymentMethod.ZarinPal);
        payment.MarkGatewayRequestStarted(DateTime.UtcNow.AddSeconds(-30));
        var gateway = new FixedZarinPalGateway(
            new PaymentGatewayRequestResult(true, "A-authority", "https://pay.example/A-authority"));

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(userId, $"buyer-{suffix}", "Test", "Buyer"),
                CreateUser(sellerId, $"seller-{suffix}", "Test", "Seller"));
            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(userId, gateway);

        Assert.ThrowsAsync<BusinessRuleException>(
            async () => await SendAsync(provider, order.Id, PaymentMethod.ZarinPal));

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == order.Id);

        Assert.Multiple(() =>
        {
            Assert.That(persistedOrder.Payments.Single().Id, Is.EqualTo(payment.Id));
            Assert.That(persistedOrder.Payments.Single().Status, Is.EqualTo(PaymentStatus.Pending));
            Assert.That(gateway.RequestCallCount, Is.Zero);
        });
    }

    [Test]
    public async Task Handle_should_retry_stale_authorityless_payment_using_the_same_payment_id()
    {
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var order = CreateOrderWithItem(userId, sellerId);
        var payment = order.InitiatePayment(PaymentMethod.ZarinPal);
        payment.MarkGatewayRequestStarted(DateTime.UtcNow.AddMinutes(-3));
        var paymentId = payment.Id;
        var authority = $"A{Guid.NewGuid():N}";
        var gateway = new FixedZarinPalGateway(
            new PaymentGatewayRequestResult(
                true,
                authority,
                "https://www.zarinpal.com/pg/StartPay/" + authority));

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(userId, $"buyer-{suffix}", "Test", "Buyer"),
                CreateUser(sellerId, $"seller-{suffix}", "Test", "Seller"));
            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(userId, gateway);
        var response = await SendAsync(provider, order.Id, PaymentMethod.ZarinPal);

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == order.Id);
        var persistedPayment = persistedOrder.Payments.Single();

        Assert.Multiple(() =>
        {
            Assert.That(response.PaymentId, Is.EqualTo(paymentId));
            Assert.That(gateway.LastPaymentId, Is.EqualTo(paymentId));
            Assert.That(gateway.RequestCallCount, Is.EqualTo(1));
            Assert.That(persistedOrder.Payments, Has.Count.EqualTo(1));
            Assert.That(persistedPayment.Id, Is.EqualTo(paymentId));
            Assert.That(persistedPayment.Status, Is.EqualTo(PaymentStatus.Pending));
            Assert.That(persistedPayment.Authority, Is.EqualTo(authority));
            Assert.That(persistedPayment.LastGatewayRequestStartedAt, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-1)));
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

    private ServiceProvider CreateProvider(Guid? currentUserId, IPaymentGateway? paymentGateway = null)
    {
        var configuration = new ConfigurationManager();

        configuration["ConnectionStrings:ECommerceDb"] = ConnectionString;
        configuration["OrderExpiration:PaymentWindowMinutes"] = "30";
        configuration["Jwt:Issuer"] = "ECommerce.Tests";
        configuration["Jwt:Audience"] = "ECommerce.Tests";
        configuration["Jwt:ExpirationMinutes"] = "60";
        configuration["Jwt:SecretKey"] = new string('t', 32);

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        if (paymentGateway is not null)
            services.AddScoped<IPaymentGateway>(_ => paymentGateway);

        services.Configure<OrderExpirationOptions>(
            configuration.GetSection(OrderExpirationOptions.SectionName));

        services.AddScoped<ICurrentUser>(
            _ => new TestCurrentUser(currentUserId));

        return services.BuildServiceProvider();
    }

    private static async Task<InitiatePaymentResponse> SendAsync(
        IServiceProvider provider,
        Guid orderId,
        PaymentMethod paymentMethod = PaymentMethod.Mock)
    {
        using var scope = provider.CreateScope();

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        return await sender.Send(
            new InitiatePaymentCommand(orderId, paymentMethod));
    }

    private sealed class ThrowingZarinPalGateway(Exception exception) : IPaymentGateway
    {
        public int RequestCallCount { get; private set; }

        public string GetPaymentUrl(string authority)
            => "https://www.zarinpal.com/pg/StartPay/" + authority;

        public Task<PaymentGatewayRequestResult> RequestPaymentAsync(
            Guid paymentId,
            Guid orderId,
            decimal amount,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCallCount++;
            return Task.FromException<PaymentGatewayRequestResult>(exception);
        }

        public Task<PaymentGatewayVerificationResult> VerifyPaymentAsync(
            decimal amount,
            string authority,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FixedZarinPalGateway(PaymentGatewayRequestResult requestResult)
        : IPaymentGateway
    {
        public int RequestCallCount { get; private set; }
        public Guid? LastPaymentId { get; private set; }
        public Guid? LastOrderId { get; private set; }
        public decimal? LastAmount { get; private set; }

        public string GetPaymentUrl(string authority)
            => "https://www.zarinpal.com/pg/StartPay/" + authority;

        public Task<PaymentGatewayRequestResult> RequestPaymentAsync(
            Guid paymentId,
            Guid orderId,
            decimal amount,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequestCallCount++;
            LastPaymentId = paymentId;
            LastOrderId = orderId;
            LastAmount = amount;
            return Task.FromResult(requestResult);
        }

        public Task<PaymentGatewayVerificationResult> VerifyPaymentAsync(
            decimal amount,
            string authority,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;

        public bool IsAuthenticated => UserId.HasValue;

        public bool IsInRole(string role) => false;
    }
}
