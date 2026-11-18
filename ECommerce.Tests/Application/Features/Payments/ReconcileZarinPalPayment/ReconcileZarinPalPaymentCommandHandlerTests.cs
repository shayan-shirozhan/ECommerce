using ECommerce.Application;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Features.Payments.ReconcileZarinPalPayment;
using ECommerce.Application.Features.Payments.Shared;
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

namespace ECommerce.Tests.Application.Features.Payments.ReconcileZarinPalPayment;

public sealed class ReconcileZarinPalPaymentCommandHandlerTests : PostgresTestBase
{
    [Test]
    public async Task Handle_should_retry_an_indeterminate_payment_before_order_expiration()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedPaymentAsync(authority, lastAttemptAtUtc: DateTime.UtcNow.AddMinutes(-4));
        var gateway = new FixedZarinPalGateway(new PaymentGatewayVerificationResult(
            false, false, null, "503", "Provider temporarily unavailable.", IsIndeterminate: true));
        using var provider = CreateProvider(gateway);

        var outcome = await SendAsync(provider, seeded.PaymentId, authority);

        await using var context = CreateDbContext();
        var order = await context.Orders.Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);
        var payment = order.Payments.Single();

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(ZarinPalVerificationOutcome.Indeterminate));
            Assert.That(order.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Pending));
            Assert.That(payment.VerificationAttemptCount, Is.EqualTo(2));
            Assert.That(payment.LastVerificationErrorCode, Is.EqualTo("503"));
            Assert.That(payment.LastVerificationAttemptedAt, Is.GreaterThan(seeded.LastAttemptAtUtc));
            Assert.That(gateway.CallCount, Is.EqualTo(1));
            Assert.That(gateway.LastAmount, Is.EqualTo(200m));
            Assert.That(gateway.LastAuthority, Is.EqualTo(authority));
        });
    }

    [Test]
    public async Task Handle_should_confirm_order_when_reconciliation_finds_success()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedPaymentAsync(authority, lastAttemptAtUtc: DateTime.UtcNow.AddMinutes(-4));
        var gateway = new FixedZarinPalGateway(new PaymentGatewayVerificationResult(true, false, 7654321));
        using var provider = CreateProvider(gateway);

        var outcome = await SendAsync(provider, seeded.PaymentId, authority);

        await using var context = CreateDbContext();
        var order = await context.Orders.Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(ZarinPalVerificationOutcome.Successful));
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Confirmed));
            Assert.That(order.Payments.Single().Status, Is.EqualTo(PaymentStatus.Successful));
            Assert.That(order.Payments.Single().RefId, Is.EqualTo(7654321));
            Assert.That(gateway.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Handle_should_mark_only_definitive_minus_51_as_failed_without_cancelling_order()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedPaymentAsync(authority, lastAttemptAtUtc: DateTime.UtcNow.AddMinutes(-4));
        var gateway = new FixedZarinPalGateway(
            new PaymentGatewayVerificationResult(false, false, null, "-51", "Payment not completed."));
        using var provider = CreateProvider(gateway);

        var outcome = await SendAsync(provider, seeded.PaymentId, authority);

        await using var context = CreateDbContext();
        var order = await context.Orders.Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(ZarinPalVerificationOutcome.Failed));
            Assert.That(order.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(order.Payments.Single().Status, Is.EqualTo(PaymentStatus.Failed));
            Assert.That(gateway.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Handle_should_require_manual_review_after_the_fifth_indeterminate_verification()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedPaymentAsync(
            authority,
            priorVerificationAttempts: 4);
        var gateway = new FixedZarinPalGateway(new PaymentGatewayVerificationResult(
            false, false, null, "-50", "Uncertain provider result.", IsIndeterminate: true));
        using var provider = CreateProvider(gateway);

        var outcome = await SendAsync(provider, seeded.PaymentId, authority);

        await using var context = CreateDbContext();
        var order = await context.Orders.Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);
        var payment = order.Payments.Single();

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(ZarinPalVerificationOutcome.RequiresManualReview));
            Assert.That(order.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(payment.Status, Is.EqualTo(PaymentStatus.RequiresManualReview));
            Assert.That(payment.VerificationAttemptCount, Is.EqualTo(5));
            Assert.That(payment.LastVerificationErrorCode, Is.EqualTo("-50"));
            Assert.That(payment.ManualReviewRequiredAt, Is.Not.Null);
            Assert.That(gateway.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Handle_should_skip_payment_while_the_verification_lease_is_active()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedPaymentAsync(authority, lastAttemptAtUtc: DateTime.UtcNow.AddSeconds(-30));
        var gateway = new FixedZarinPalGateway(new PaymentGatewayVerificationResult(true, false, 7654321));
        using var provider = CreateProvider(gateway);

        var outcome = await SendAsync(provider, seeded.PaymentId, authority);

        await using var context = CreateDbContext();
        var order = await context.Orders.Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);

        Assert.Multiple(() =>
        {
            Assert.That(outcome, Is.EqualTo(ZarinPalVerificationOutcome.Skipped));
            Assert.That(order.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(order.Payments.Single().Status, Is.EqualTo(PaymentStatus.Pending));
            Assert.That(order.Payments.Single().VerificationAttemptCount, Is.EqualTo(1));
            Assert.That(gateway.CallCount, Is.Zero);
        });
    }

    [Test]
    public async Task Repository_should_return_only_due_pending_zarinpal_payments_that_have_been_verified()
    {
        var dueAuthority = $"A{Guid.NewGuid():N}";
        var due = await SeedPaymentAsync(dueAuthority, lastAttemptAtUtc: DateTime.UtcNow.AddMinutes(-4));
        await SeedPaymentAsync($"A{Guid.NewGuid():N}", lastAttemptAtUtc: DateTime.UtcNow.AddSeconds(-30));
        await SeedPaymentAsync($"A{Guid.NewGuid():N}");

        await using var context = CreateDbContext();
        var repository = new OrderRepository(context);
        var candidates = await repository.GetPendingZarinPalPaymentsForReconciliationAsync(
            DateTime.UtcNow.Subtract(PaymentGatewayRetryPolicy.RetryDelay), take: 100);

        Assert.That(candidates, Has.Count.EqualTo(1));
        Assert.That(candidates.Single().PaymentId, Is.EqualTo(due.PaymentId));
        Assert.That(candidates.Single().Authority, Is.EqualTo(dueAuthority));
    }

    private async Task<SeededPayment> SeedPaymentAsync(
        string authority,
        DateTime? lastAttemptAtUtc = null,
        int priorVerificationAttempts = 0)
    {
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var order = Order.Create(buyerId, "Test Buyer", "09120000000", "Tehran", "Tehran", "1234567890", "Test Street");
        order.AddItem(sellerId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Product", "Default", "Test Seller", 200m, 1, 0m);

        var payment = order.InitiatePayment(PaymentMethod.ZarinPal);
        payment.SetAuthority(authority);
        for (var attempt = priorVerificationAttempts; attempt > 0; attempt--)
            payment.MarkVerificationAttempted(DateTime.UtcNow.AddMinutes(-attempt * 3));
        if (lastAttemptAtUtc.HasValue)
            payment.MarkVerificationAttempted(lastAttemptAtUtc.Value);

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(CreateUser(buyerId, $"buyer-{suffix}"), CreateUser(sellerId, $"seller-{suffix}"));
            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        return new SeededPayment(order.Id, payment.Id, lastAttemptAtUtc ?? DateTime.MinValue);
    }

    private ServiceProvider CreateProvider(IPaymentGateway gateway)
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
        services.AddSingleton<IPaymentGateway>(gateway);
        services.Configure<OrderExpirationOptions>(configuration.GetSection(OrderExpirationOptions.SectionName));
        return services.BuildServiceProvider();
    }

    private static async Task<ZarinPalVerificationOutcome> SendAsync(IServiceProvider provider, Guid paymentId, string authority)
    {
        using var scope = provider.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new ReconcileZarinPalPaymentCommand(paymentId, authority));
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

    private sealed record SeededPayment(Guid OrderId, Guid PaymentId, DateTime LastAttemptAtUtc);

    private sealed class FixedZarinPalGateway(PaymentGatewayVerificationResult result) : IPaymentGateway
    {
        public int CallCount { get; private set; }
        public decimal LastAmount { get; private set; }
        public string? LastAuthority { get; private set; }
        public string GetPaymentUrl(string authority) => "https://www.zarinpal.com/pg/StartPay/" + authority;
        public Task<PaymentGatewayRequestResult> RequestPaymentAsync(Guid paymentId, Guid orderId, decimal amount, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<PaymentGatewayVerificationResult> VerifyPaymentAsync(decimal amount, string authority, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastAmount = amount;
            LastAuthority = authority;
            return Task.FromResult(result);
        }
    }
}
