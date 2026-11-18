using ECommerce.Application;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Orders.ExpireOrders;
using ECommerce.Application.Options;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Infrastructure.Persistence.Repositories;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Orders.ExpireOrders;

public sealed class ExpireOrderCommandHandlerTests : PostgresTestBase
{
    [Test]
    public async Task Handle_should_return_false_when_no_order_is_expired()
    {
        var seeded = await SeedOrderAsync();
        using var provider = CreateProvider();

        var processed = await SendAsync(
            provider,
            seeded.OrderId,
            DateTime.UtcNow.AddMinutes(-1));

        await using var context = CreateDbContext();
        var order = await context.Orders.SingleAsync(x => x.Id == seeded.OrderId);
        var offer = await context.SellerOffers.SingleAsync(x => x.Id == seeded.OfferId);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.False);
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Pending));
            Assert.That(offer.Stock, Is.EqualTo(8));
        });
    }

    [Test]
    public async Task Handle_should_cancel_expired_pending_order_and_restore_stock()
    {
        var seeded = await SeedOrderAsync();
        using var provider = CreateProvider();

        var processed = await SendAsync(
            provider,
            seeded.OrderId,
            DateTime.UtcNow.AddMinutes(1));

        await using var context = CreateDbContext();
        var order = await context.Orders
            .Include(x => x.Groups)
            .SingleAsync(x => x.Id == seeded.OrderId);
        var offer = await context.SellerOffers.SingleAsync(x => x.Id == seeded.OfferId);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.True);
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Cancelled));
            Assert.That(order.Groups.Single().Status, Is.EqualTo(OrderGroupStatus.Cancelled));
            Assert.That(offer.Stock, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task Handle_should_cancel_pending_payment_and_preserve_failed_payment_history()
    {
        var seeded = await SeedOrderAsync(withPaymentHistory: true);
        using var provider = CreateProvider();

        var processed = await SendAsync(
            provider,
            seeded.OrderId,
            DateTime.UtcNow.AddMinutes(1));

        await using var context = CreateDbContext();
        var order = await context.Orders
            .Include(x => x.Payments)
            .SingleAsync(x => x.Id == seeded.OrderId);
        var offer = await context.SellerOffers.SingleAsync(x => x.Id == seeded.OfferId);

        var failedPayment = order.Payments.Single(x => x.Id == seeded.FailedPaymentId);
        var pendingPayment = order.Payments.Single(x => x.Id == seeded.PendingPaymentId);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.True);
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Cancelled));
            Assert.That(failedPayment.Status, Is.EqualTo(PaymentStatus.Failed));
            Assert.That(pendingPayment.Status, Is.EqualTo(PaymentStatus.Cancelled));
            Assert.That(offer.Stock, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task Handle_should_confirm_zarinpal_order_when_expiration_reconciliation_finds_successful_payment()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedZarinPalOrderAsync(authority);
        var gateway = new FixedPaymentGateway(
            new PaymentGatewayVerificationResult(true, false, 987654321));
        using var provider = CreateProvider(gateway);

        var processed = await SendAsync(
            provider,
            seeded.OrderId,
            DateTime.UtcNow.AddMinutes(1));

        await using var context = CreateDbContext();
        var order = await context.Orders
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);
        var offer = await context.SellerOffers.SingleAsync(entity => entity.Id == seeded.OfferId);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.False);
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Confirmed));
            Assert.That(order.Payments.Single().Status, Is.EqualTo(PaymentStatus.Successful));
            Assert.That(order.Payments.Single().RefId, Is.EqualTo(987654321));
            Assert.That(order.Payments.Single().LastVerificationAttemptedAt, Is.Not.Null);
            Assert.That(offer.Stock, Is.EqualTo(8));
            Assert.That(gateway.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Handle_should_cancel_zarinpal_order_when_reconciliation_confirms_payment_failure()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedZarinPalOrderAsync(authority);
        var gateway = new FixedPaymentGateway(
            new PaymentGatewayVerificationResult(false, false, null, "-51", "Payment not completed"));
        using var provider = CreateProvider(gateway);

        var processed = await SendAsync(
            provider,
            seeded.OrderId,
            DateTime.UtcNow.AddMinutes(1));

        await using var context = CreateDbContext();
        var order = await context.Orders
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);
        var offer = await context.SellerOffers.SingleAsync(entity => entity.Id == seeded.OfferId);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.True);
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Cancelled));
            Assert.That(order.Payments.Single().Status, Is.EqualTo(PaymentStatus.Failed));
            Assert.That(offer.Stock, Is.EqualTo(10));
            Assert.That(gateway.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Handle_should_keep_expired_zarinpal_order_and_reserved_stock_when_verification_is_indeterminate()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedZarinPalOrderAsync(authority);
        var gateway = new FixedPaymentGateway(
            new PaymentGatewayVerificationResult(
                false,
                false,
                null,
                "503",
                "Provider temporarily unavailable.",
                IsIndeterminate: true));
        using var provider = CreateProvider(gateway);

        var processed = await SendAsync(
            provider,
            seeded.OrderId,
            DateTime.UtcNow.AddMinutes(1));

        await using var context = CreateDbContext();
        var order = await context.Orders
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);
        var offer = await context.SellerOffers.SingleAsync(entity => entity.Id == seeded.OfferId);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.False);
            Assert.That(order.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(order.Payments.Single().Status, Is.EqualTo(PaymentStatus.Pending));
            Assert.That(order.Payments.Single().VerificationAttemptCount, Is.EqualTo(1));
            Assert.That(order.Payments.Single().LastVerificationAttemptedAt, Is.Not.Null);
            Assert.That(offer.Stock, Is.EqualTo(8));
            Assert.That(gateway.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Handle_should_keep_stock_reserved_and_mark_manual_review_after_maximum_indeterminate_attempts()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedZarinPalOrderAsync(authority, priorVerificationAttempts: 4);
        var gateway = new FixedPaymentGateway(
            new PaymentGatewayVerificationResult(false, false, null, "-50", "Amount mismatch", IsIndeterminate: true));
        using var provider = CreateProvider(gateway);

        var processed = await SendAsync(provider, seeded.OrderId, DateTime.UtcNow.AddMinutes(1));

        await using var context = CreateDbContext();
        var order = await context.Orders
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);
        var offer = await context.SellerOffers.SingleAsync(entity => entity.Id == seeded.OfferId);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.False);
            Assert.That(order.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(order.Payments.Single().Status, Is.EqualTo(PaymentStatus.RequiresManualReview));
            Assert.That(order.Payments.Single().VerificationAttemptCount, Is.EqualTo(5));
            Assert.That(order.Payments.Single().LastVerificationErrorCode, Is.EqualTo("-50"));
            Assert.That(offer.Stock, Is.EqualTo(8));
            Assert.That(gateway.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Handle_should_require_manual_review_when_verification_age_limit_is_reached()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedZarinPalOrderAsync(
            authority,
            DateTime.UtcNow.AddMinutes(-21));
        var gateway = new FixedPaymentGateway(
            new PaymentGatewayVerificationResult(false, false, null, "503", "Unknown result", IsIndeterminate: true));
        using var provider = CreateProvider(gateway);

        var processed = await SendAsync(provider, seeded.OrderId, DateTime.UtcNow.AddMinutes(1));

        await using var context = CreateDbContext();
        var order = await context.Orders
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);
        var offer = await context.SellerOffers.SingleAsync(entity => entity.Id == seeded.OfferId);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.False);
            Assert.That(order.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(order.Payments.Single().Status, Is.EqualTo(PaymentStatus.RequiresManualReview));
            Assert.That(order.Payments.Single().VerificationAttemptCount, Is.EqualTo(1));
            Assert.That(offer.Stock, Is.EqualTo(8));
            Assert.That(gateway.CallCount, Is.Zero);
        });
    }

    [Test]
    public async Task GetExpiredOrderIdsAsync_should_use_latest_payment_created_at_as_expiration_anchor()
    {
        var seeded = await SeedOrderAsync();

        await using (var context = CreateDbContext())
        {
            var order = await context.Orders
                .Include(entity => entity.Groups)
                .ThenInclude(group => group.Items)
                .Include(entity => entity.Payments)
                .SingleAsync(entity => entity.Id == seeded.OrderId);

            context.Entry(order).Property(entity => entity.CreatedAt).CurrentValue = DateTime.UtcNow.AddMinutes(-31);
            order.InitiatePayment(PaymentMethod.ZarinPal).SetAuthority($"A{Guid.NewGuid():N}");
            await context.SaveChangesAsync();
        }

        await using var queryContext = CreateDbContext();
        var repository = new OrderRepository(queryContext);
        var nowUtc = DateTime.UtcNow;
        var expiredIds = await repository.GetExpiredOrderIdsAsync(
            nowUtc.Subtract(TimeSpan.FromMinutes(30)),
            nowUtc.Subtract(PaymentGatewayRetryPolicy.RetryDelay),
            take: 100);

        Assert.That(expiredIds, Does.Not.Contain(seeded.OrderId));
    }

    [Test]
    public async Task Handle_should_respect_verification_retry_delay_before_reconciling_expired_order()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedZarinPalOrderAsync(
            authority,
            DateTime.UtcNow.AddSeconds(-30));
        var gateway = new FixedPaymentGateway(
            new PaymentGatewayVerificationResult(true, false, 987654321));
        using var provider = CreateProvider(gateway);

        var processed = await SendAsync(
            provider,
            seeded.OrderId,
            DateTime.UtcNow.AddMinutes(1));

        await using var context = CreateDbContext();
        var order = await context.Orders
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.False);
            Assert.That(order.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(order.Payments.Single().Status, Is.EqualTo(PaymentStatus.Pending));
            Assert.That(gateway.CallCount, Is.Zero);
        });
    }

    [Test]
    public async Task Handle_should_not_expire_order_while_gateway_request_lease_is_active_without_authority()
    {
        var seeded = await SeedOrderAsync();

        await using (var context = CreateDbContext())
        {
            var order = await context.Orders
                .Include(entity => entity.Groups)
                .ThenInclude(group => group.Items)
                .Include(entity => entity.Payments)
                .SingleAsync(entity => entity.Id == seeded.OrderId);

            var payment = order.InitiatePayment(PaymentMethod.ZarinPal);
            payment.MarkGatewayRequestStarted(DateTime.UtcNow.AddSeconds(-30));
            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider();
        var processed = await SendAsync(
            provider,
            seeded.OrderId,
            DateTime.UtcNow.AddMinutes(1));

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);
        var offer = await verificationContext.SellerOffers
            .SingleAsync(entity => entity.Id == seeded.OfferId);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.False);
            Assert.That(persistedOrder.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(persistedOrder.Payments.Single().Status, Is.EqualTo(PaymentStatus.Pending));
            Assert.That(persistedOrder.Payments.Single().Authority, Is.Null);
            Assert.That(offer.Stock, Is.EqualTo(8));
        });
    }

    [Test]
    public async Task Handle_should_cancel_zarinpal_payment_without_authority_after_gateway_request_lease_expires()
    {
        var seeded = await SeedOrderAsync();

        await using (var context = CreateDbContext())
        {
            var order = await context.Orders
                .Include(entity => entity.Groups)
                .ThenInclude(group => group.Items)
                .Include(entity => entity.Payments)
                .SingleAsync(entity => entity.Id == seeded.OrderId);

            var payment = order.InitiatePayment(PaymentMethod.ZarinPal);
            payment.MarkGatewayRequestStarted(DateTime.UtcNow.AddMinutes(-3));
            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider();
        var processed = await SendAsync(
            provider,
            seeded.OrderId,
            DateTime.UtcNow.AddMinutes(1));

        await using var verificationContext = CreateDbContext();
        var orderAfterExpiration = await verificationContext.Orders
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);
        var offer = await verificationContext.SellerOffers
            .SingleAsync(entity => entity.Id == seeded.OfferId);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.True);
            Assert.That(orderAfterExpiration.Status, Is.EqualTo(OrderStatus.Cancelled));
            Assert.That(orderAfterExpiration.Payments.Single().Status, Is.EqualTo(PaymentStatus.Cancelled));
            Assert.That(offer.Stock, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task GetExpiredOrderIdsAsync_should_skip_orders_with_recent_gateway_request_without_authority()
    {
        var seeded = await SeedOrderAsync();

        await using (var context = CreateDbContext())
        {
            var order = await context.Orders
                .Include(entity => entity.Groups)
                .ThenInclude(group => group.Items)
                .Include(entity => entity.Payments)
                .SingleAsync(entity => entity.Id == seeded.OrderId);

            var payment = order.InitiatePayment(PaymentMethod.ZarinPal);
            payment.MarkGatewayRequestStarted(DateTime.UtcNow.AddSeconds(-30));
            await context.SaveChangesAsync();
        }

        await using var queryContext = CreateDbContext();
        var repository = new OrderRepository(queryContext);
        var nowUtc = DateTime.UtcNow;
        var result = await repository.GetExpiredOrderIdsAsync(
            nowUtc.AddMinutes(1),
            nowUtc.Subtract(PaymentGatewayRetryPolicy.RetryDelay),
            take: 100);

        Assert.That(result, Does.Not.Contain(seeded.OrderId));
    }

    [Test]
    public async Task GetExpiredOrderIdsAsync_should_skip_orders_with_recent_zarinpal_verification_attempts()
    {
        var recentlyVerifiedOrder = await SeedZarinPalOrderAsync(
            $"A{Guid.NewGuid():N}",
            DateTime.UtcNow.AddSeconds(-30));
        var regularExpiredOrder = await SeedOrderAsync();

        await using var context = CreateDbContext();
        var repository = new OrderRepository(context);
        var nowUtc = DateTime.UtcNow;

        var result = await repository.GetExpiredOrderIdsAsync(
            nowUtc.AddMinutes(1),
            nowUtc.Subtract(PaymentGatewayRetryPolicy.RetryDelay),
            take: 100);

        Assert.Multiple(() =>
        {
            Assert.That(result, Does.Contain(regularExpiredOrder.OrderId));
            Assert.That(result, Does.Not.Contain(recentlyVerifiedOrder.OrderId));
        });
    }

    [Test]
    public async Task Handle_should_not_expire_confirmed_order()
    {
        var seeded = await SeedOrderAsync(confirmPayment: true);
        using var provider = CreateProvider();

        var processed = await SendAsync(
            provider,
            seeded.OrderId,
            DateTime.UtcNow.AddMinutes(1));

        await using var context = CreateDbContext();
        var order = await context.Orders
            .Include(x => x.Payments)
            .SingleAsync(x => x.Id == seeded.OrderId);
        var offer = await context.SellerOffers.SingleAsync(x => x.Id == seeded.OfferId);

        Assert.Multiple(() =>
        {
            Assert.That(processed, Is.False);
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Confirmed));
            Assert.That(order.Payments.Single().Status, Is.EqualTo(PaymentStatus.Successful));
            Assert.That(offer.Stock, Is.EqualTo(8));
        });
    }

    [Test]
    public async Task Handle_should_restore_stock_only_once_when_expiration_is_requested_repeatedly()
    {
        var seeded = await SeedOrderAsync();
        using var provider = CreateProvider();

        var cutoffUtc = DateTime.UtcNow.AddMinutes(1);

        var firstResult = await SendAsync(provider, seeded.OrderId, cutoffUtc);
        var secondResult = await SendAsync(provider, seeded.OrderId, cutoffUtc);

        await using var context = CreateDbContext();
        var offer = await context.SellerOffers.SingleAsync(x => x.Id == seeded.OfferId);

        Assert.Multiple(() =>
        {
            Assert.That(firstResult, Is.True);
            Assert.That(secondResult, Is.False);
            Assert.That(offer.Stock, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task Handle_should_restore_stock_only_once_for_concurrent_expiration_requests()
    {
        var seeded = await SeedOrderAsync();
        using var provider = CreateProvider();

        var cutoffUtc = DateTime.UtcNow.AddMinutes(1);

        var outcomes = await Task.WhenAll(
            CaptureOutcomeAsync(provider, seeded.OrderId, cutoffUtc),
            CaptureOutcomeAsync(provider, seeded.OrderId, cutoffUtc));

        await using var context = CreateDbContext();
        var order = await context.Orders.SingleAsync(x => x.Id == seeded.OrderId);
        var offer = await context.SellerOffers.SingleAsync(x => x.Id == seeded.OfferId);

        Assert.Multiple(() =>
        {
            Assert.That(
                outcomes.Count(x => x.Processed == true),
                Is.EqualTo(1));

            Assert.That(
                outcomes.All(x =>
                    x.Exception is null or ConcurrencyConflictException or PersistenceConflictException),
                Is.True);

            Assert.That(order.Status, Is.EqualTo(OrderStatus.Cancelled));
            Assert.That(offer.Stock, Is.EqualTo(10));
        });
    }

    private async Task<SeededOrder> SeedOrderAsync(
        bool withPaymentHistory = false,
        bool confirmPayment = false)
    {
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var brand = Brand.Create($"Expiration Brand {suffix}");
        var category = Category.Create($"Expiration Category {suffix}");

        var product = Product.Create(
            $"Expiration Product {suffix}",
            "Product used by expiration tests.",
            brand.Id,
            category.Id);

        product.Activate();

        var variant = ProductVariant.Create(
            product,
            $"EXP-{suffix}",
            "Default");

        variant.Activate(product);

        // Two units have already been reserved by the order.
        var offer = SellerOffer.Create(sellerId, variant.Id, 100m, stock: 8);
        offer.Activate();

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
            product.Id,
            variant.Id,
            offer.Id,
            product.Name,
            variant.Name ?? "Default",
            "Test Seller",
            100m,
            2,
            0m);

        Guid? failedPaymentId = null;
        Guid? pendingPaymentId = null;

        if (withPaymentHistory)
        {
            var failedPayment = order.InitiatePayment();
            failedPayment.MarkFailed();

            failedPaymentId = failedPayment.Id;

            var pendingPayment = order.InitiatePayment();
            pendingPaymentId = pendingPayment.Id;
        }
        else if (confirmPayment)
        {
            var payment = order.InitiatePayment();
            payment.MarkSuccessful($"expiration-txn-{Guid.NewGuid():N}");
            order.Confirm();
        }

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(buyerId, $"buyer-{suffix}"),
                CreateUser(sellerId, $"seller-{suffix}"));

            context.AddRange(
                brand,
                category,
                product,
                variant,
                offer,
                order);

            await context.SaveChangesAsync();
        }

        return new SeededOrder(
            order.Id,
            offer.Id,
            failedPaymentId,
            pendingPaymentId);
    }

    private async Task<SeededOrder> SeedZarinPalOrderAsync(
        string authority,
        DateTime? lastVerificationAttemptedAt = null,
        int priorVerificationAttempts = 0)
    {
        var seeded = await SeedOrderAsync();

        await using var context = CreateDbContext();
        var order = await context.Orders
            .Include(entity => entity.Groups)
            .ThenInclude(group => group.Items)
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);

        var payment = order.InitiatePayment(PaymentMethod.ZarinPal);
        payment.MarkGatewayRequestStarted(DateTime.UtcNow.AddMinutes(-3));
        payment.SetAuthority(authority);
        for (var attempt = priorVerificationAttempts; attempt > 0; attempt--)
        {
            payment.MarkVerificationAttempted(DateTime.UtcNow.AddMinutes(-attempt * 3));
        }

        if (lastVerificationAttemptedAt.HasValue)
            payment.MarkVerificationAttempted(lastVerificationAttemptedAt.Value);

        await context.SaveChangesAsync();

        return seeded with { PendingPaymentId = payment.Id };
    }

    private static ApplicationUser CreateUser(Guid id, string userName)
    {
        var email = $"{userName}@example.test";

        return new ApplicationUser
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = "Test",
            LastName = "User"
        };
    }

    private ServiceProvider CreateProvider(IPaymentGateway? paymentGateway = null)
    {
        var configuration = new ConfigurationManager();

        configuration["ConnectionStrings:ECommerceDb"] = ConnectionString;
        configuration["Jwt:Issuer"] = "ECommerce.Tests";
        configuration["Jwt:Audience"] = "ECommerce.Tests";
        configuration["Jwt:ExpirationMinutes"] = "60";
        configuration["Jwt:SecretKey"] = new string('t', 32);
        configuration["OrderExpiration:MaxVerificationAttempts"] = "5";
        configuration["OrderExpiration:MaxVerificationAgeMinutes"] = "20";

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        if (paymentGateway is not null)
            services.AddSingleton<IPaymentGateway>(paymentGateway);

        services.Configure<OrderExpirationOptions>(
            configuration.GetSection(OrderExpirationOptions.SectionName));

        return services.BuildServiceProvider();
    }

    private static async Task<bool> SendAsync(
        IServiceProvider provider,
        Guid orderId,
        DateTime cutoffUtc)
    {
        using var scope = provider.CreateScope();

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        return await sender.Send(
            new ExpireOrderCommand(orderId, cutoffUtc));
    }

    private static async Task<ExpirationOutcome> CaptureOutcomeAsync(
        IServiceProvider provider,
        Guid orderId,
        DateTime cutoffUtc)
    {
        try
        {
            var result = await SendAsync(provider, orderId, cutoffUtc);
            return new ExpirationOutcome(result, null);
        }
        catch (Exception exception)
        {
            return new ExpirationOutcome(null, exception);
        }
    }

    private sealed class FixedPaymentGateway(
        PaymentGatewayVerificationResult verificationResult) : IPaymentGateway
    {
        public int CallCount { get; private set; }

        public string GetPaymentUrl(string authority)
            => "https://www.zarinpal.com/pg/StartPay/" + authority;

        public Task<PaymentGatewayRequestResult> RequestPaymentAsync(
            Guid paymentId,
            Guid orderId,
            decimal amount,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<PaymentGatewayVerificationResult> VerifyPaymentAsync(
            decimal amount,
            string authority,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(verificationResult);
        }
    }

    private sealed record SeededOrder(
        Guid OrderId,
        Guid OfferId,
        Guid? FailedPaymentId,
        Guid? PendingPaymentId);

    private sealed record ExpirationOutcome(
        bool? Processed,
        Exception? Exception);
}
