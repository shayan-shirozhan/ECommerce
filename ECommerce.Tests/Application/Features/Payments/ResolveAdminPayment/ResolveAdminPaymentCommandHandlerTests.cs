using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Payments.ResolveAdminPayment;
using ECommerce.Application.Features.Payments.Shared;
using ECommerce.Application.Options;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Payments.ResolveAdminPayment;

public sealed class ResolveAdminPaymentCommandHandlerTests : PostgresTestBase
{
    [Test]
    public async Task Handle_should_approve_manual_review_payment_and_confirm_order_with_audit()
    {
        var seeded = await SeedManualReviewPaymentAsync();
        using var provider = CreateProvider(seeded.AdminUserId);

        var response = await SendAsync(
            provider,
            seeded.PaymentId,
            PaymentResolutionAction.Approve,
            "Matched provider settlement report.",
            refId: 987654321);

        await using var context = CreateDbContext();
        var order = await context.Orders
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);
        var audit = await context.PaymentResolutionAudits.SingleAsync(entity => entity.PaymentId == seeded.PaymentId);

        Assert.Multiple(() =>
        {
            Assert.That(response.PaymentStatus, Is.EqualTo(PaymentStatus.Successful));
            Assert.That(response.OrderStatus, Is.EqualTo(OrderStatus.Confirmed));
            Assert.That(response.RefId, Is.EqualTo(987654321));
            Assert.That(response.VerificationOutcome, Is.Null);
            Assert.That(order.Payments.Single().VerificationAttemptCount, Is.EqualTo(5));
            Assert.That(audit.AdminUserId, Is.EqualTo(seeded.AdminUserId));
            Assert.That(audit.PaymentId, Is.EqualTo(seeded.PaymentId));
            Assert.That(audit.Action, Is.EqualTo(PaymentResolutionAction.Approve));
            Assert.That(audit.Reason, Is.EqualTo("Matched provider settlement report."));
            Assert.That(audit.RefId, Is.EqualTo(987654321));
            Assert.That(audit.Outcome, Is.EqualTo("Approved"));
            Assert.That(audit.CompletedAtUtc, Is.Not.Null);
        });
    }

    [Test]
    public async Task Handle_should_reject_payment_cancel_order_restore_inventory_and_audit_atomically()
    {
        var seeded = await SeedManualReviewPaymentAsync();
        using var provider = CreateProvider(seeded.AdminUserId);

        var response = await SendAsync(
            provider,
            seeded.PaymentId,
            PaymentResolutionAction.RejectAndCancel,
            "Provider confirmed that the transaction was not paid.");

        await using var context = CreateDbContext();
        var order = await context.Orders
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);
        var offer = await context.SellerOffers.SingleAsync(entity => entity.Id == seeded.SellerOfferId);
        var audit = await context.PaymentResolutionAudits.SingleAsync(entity => entity.PaymentId == seeded.PaymentId);

        Assert.Multiple(() =>
        {
            Assert.That(response.PaymentStatus, Is.EqualTo(PaymentStatus.Failed));
            Assert.That(response.OrderStatus, Is.EqualTo(OrderStatus.Cancelled));
            Assert.That(response.RestoredItemCount, Is.EqualTo(1));
            Assert.That(response.RestoredQuantity, Is.EqualTo(2));
            Assert.That(order.Payments.Single().Status, Is.EqualTo(PaymentStatus.Failed));
            Assert.That(offer.Stock, Is.EqualTo(10));
            Assert.That(audit.Outcome, Is.EqualTo("RejectedAndCancelled"));
            Assert.That(audit.PaymentStatusAfter, Is.EqualTo(PaymentStatus.Failed));
            Assert.That(audit.OrderStatusAfter, Is.EqualTo(OrderStatus.Cancelled));
            Assert.That(audit.RestoredItemCount, Is.EqualTo(1));
            Assert.That(audit.RestoredQuantity, Is.EqualTo(2));
            Assert.That(audit.CompletedAtUtc, Is.Not.Null);
        });
    }

    [Test]
    public async Task Handle_should_retry_verification_past_automatic_limit_and_confirm_on_provider_success()
    {
        var seeded = await SeedManualReviewPaymentAsync();
        var gateway = new FixedZarinPalGateway(
            new PaymentGatewayVerificationResult(true, false, 123456789));
        using var provider = CreateProvider(seeded.AdminUserId, gateway);

        var response = await SendAsync(
            provider,
            seeded.PaymentId,
            PaymentResolutionAction.VerifyAgain,
            "Manual reconciliation requested after reviewing the merchant dashboard.");

        await using var context = CreateDbContext();
        var order = await context.Orders
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);
        var payment = order.Payments.Single();
        var audit = await context.PaymentResolutionAudits.SingleAsync(entity => entity.PaymentId == seeded.PaymentId);

        Assert.Multiple(() =>
        {
            Assert.That(gateway.CallCount, Is.EqualTo(1));
            Assert.That(response.VerificationOutcome, Is.EqualTo(ZarinPalVerificationOutcome.Successful));
            Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Successful));
            Assert.That(payment.VerificationAttemptCount, Is.EqualTo(6));
            Assert.That(payment.FirstVerificationAttemptedAt, Is.EqualTo(seeded.FirstVerificationAttemptedAt).Within(TimeSpan.FromMilliseconds(1)));
            Assert.That(payment.RefId, Is.EqualTo(123456789));
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Confirmed));
            Assert.That(audit.Outcome, Is.EqualTo("Successful"));
            Assert.That(audit.PaymentStatusAfter, Is.EqualTo(PaymentStatus.Successful));
            Assert.That(audit.OrderStatusAfter, Is.EqualTo(OrderStatus.Confirmed));
            Assert.That(audit.RefId, Is.EqualTo(123456789));
        });
    }

    [Test]
    public async Task Handle_should_mark_definitive_provider_rejection_failed_without_cancelling_order()
    {
        var seeded = await SeedManualReviewPaymentAsync();
        var gateway = new FixedZarinPalGateway(new PaymentGatewayVerificationResult(
            false, false, null, "-51", "The payment was not completed."));
        using var provider = CreateProvider(seeded.AdminUserId, gateway);

        var response = await SendAsync(
            provider,
            seeded.PaymentId,
            PaymentResolutionAction.VerifyAgain,
            "Provider verification returned definitive non-payment code -51.");

        await using var context = CreateDbContext();
        var order = await context.Orders.Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);
        var audit = await context.PaymentResolutionAudits
            .SingleAsync(entity => entity.PaymentId == seeded.PaymentId);

        Assert.Multiple(() =>
        {
            Assert.That(response.VerificationOutcome, Is.EqualTo(ZarinPalVerificationOutcome.Failed));
            Assert.That(order.Payments.Single().Status, Is.EqualTo(PaymentStatus.Failed));
            Assert.That(order.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(audit.ProviderErrorCode, Is.EqualTo("-51"));
            Assert.That(audit.PaymentStatusAfter, Is.EqualTo(PaymentStatus.Failed));
        });

        var newPayment = order.InitiatePayment(PaymentMethod.ZarinPal);
        Assert.That(newPayment.Status, Is.EqualTo(PaymentStatus.Pending));
    }

    [Test]
    public async Task Handle_should_return_to_manual_review_when_admin_verification_remains_indeterminate()
    {
        var seeded = await SeedManualReviewPaymentAsync();
        var gateway = new FixedZarinPalGateway(new PaymentGatewayVerificationResult(
            false, false, null, "503", "Provider unavailable.", IsIndeterminate: true));
        using var provider = CreateProvider(seeded.AdminUserId, gateway);

        var response = await SendAsync(
            provider,
            seeded.PaymentId,
            PaymentResolutionAction.VerifyAgain,
            "Retry after checking the provider status page.");

        await using var context = CreateDbContext();
        var order = await context.Orders.Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);
        var payment = order.Payments.Single();
        var audit = await context.PaymentResolutionAudits.SingleAsync(entity => entity.PaymentId == seeded.PaymentId);

        Assert.Multiple(() =>
        {
            Assert.That(response.VerificationOutcome, Is.EqualTo(ZarinPalVerificationOutcome.RequiresManualReview));
            Assert.That(payment.Status, Is.EqualTo(PaymentStatus.RequiresManualReview));
            Assert.That(payment.VerificationAttemptCount, Is.EqualTo(6));
            Assert.That(payment.LastVerificationErrorCode, Is.EqualTo("503"));
            Assert.That(order.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(audit.Outcome, Is.EqualTo("RequiresManualReview"));
            Assert.That(audit.ProviderErrorCode, Is.EqualTo("503"));
        });
    }

    [Test]
    public async Task Handle_should_reject_verify_again_while_previous_verification_lease_is_active()
    {
        var seeded = await SeedManualReviewPaymentAsync(lastAttemptAge: TimeSpan.FromSeconds(30));
        var gateway = new FixedZarinPalGateway(new PaymentGatewayVerificationResult(true, false, 123456789));
        using var provider = CreateProvider(seeded.AdminUserId, gateway);

        var exception = Assert.ThrowsAsync<BusinessRuleException>(async () =>
            await SendAsync(
                provider,
                seeded.PaymentId,
                PaymentResolutionAction.VerifyAgain,
                "Do not overlap provider requests."));

        await using var context = CreateDbContext();
        var payment = await context.Payments.SingleAsync(entity => entity.Id == seeded.PaymentId);
        var auditCount = await context.PaymentResolutionAudits.CountAsync(entity => entity.PaymentId == seeded.PaymentId);

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("two-minute lease"));
            Assert.That(gateway.CallCount, Is.Zero);
            Assert.That(payment.Status, Is.EqualTo(PaymentStatus.RequiresManualReview));
            Assert.That(payment.VerificationAttemptCount, Is.EqualTo(5));
            Assert.That(auditCount, Is.Zero);
        });
    }

    [Test]
    public async Task Handle_should_keep_payment_in_manual_review_and_complete_audit_when_gateway_is_not_configured()
    {
        var seeded = await SeedManualReviewPaymentAsync();
        using var provider = CreateProvider(seeded.AdminUserId);

        Assert.ThrowsAsync<PaymentGatewayException>(async () =>
            await SendAsync(
                provider,
                seeded.PaymentId,
                PaymentResolutionAction.VerifyAgain,
                "Retry requires the configured provider."));

        await using var context = CreateDbContext();
        var order = await context.Orders.Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);
        var audit = await context.PaymentResolutionAudits.SingleAsync(entity => entity.PaymentId == seeded.PaymentId);

        Assert.Multiple(() =>
        {
            Assert.That(order.Payments.Single().Status, Is.EqualTo(PaymentStatus.RequiresManualReview));
            Assert.That(order.Payments.Single().VerificationAttemptCount, Is.EqualTo(5));
            Assert.That(audit.Outcome, Is.EqualTo("GatewayError"));
            Assert.That(audit.CompletedAtUtc, Is.Not.Null);
        });
    }

    [Test]
    public async Task Handle_should_reject_unauthenticated_caller()
    {
        var seeded = await SeedManualReviewPaymentAsync();
        using var provider = CreateProvider(null);

        Assert.ThrowsAsync<UnauthorizedException>(async () =>
            await SendAsync(
                provider,
                seeded.PaymentId,
                PaymentResolutionAction.Approve,
                "Manual approval.",
                refId: 456789));
    }

    private async Task<SeededPayment> SeedManualReviewPaymentAsync(TimeSpan? lastAttemptAge = null)
    {
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var brand = Brand.Create($"Resolution Brand {suffix}");
        var category = Category.Create($"Resolution Category {suffix}");
        var product = Product.Create(
            $"Resolution Product {suffix}",
            "Product used by admin payment resolution tests.",
            brand.Id,
            category.Id);
        product.Activate();

        var variant = ProductVariant.Create(product, $"RES-{suffix}", "Default");
        variant.Activate(product);

        // The order has already reserved two units; eight remain in stock.
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

        var payment = order.InitiatePayment(PaymentMethod.ZarinPal);
        payment.SetAuthority($"A{Guid.NewGuid():N}");

        for (var attempt = 5; attempt >= 1; attempt--)
        {
            var age = attempt == 1 ? lastAttemptAge ?? TimeSpan.FromMinutes(3) : TimeSpan.FromMinutes(attempt * 3);
            payment.MarkVerificationAttempted(DateTime.UtcNow.Subtract(age));
        }

        payment.MarkRequiresManualReview();
        var firstAttemptAt = payment.FirstVerificationAttemptedAt!.Value;

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(buyerId, $"buyer-{suffix}"),
                CreateUser(sellerId, $"seller-{suffix}"),
                CreateUser(adminUserId, $"admin-{suffix}"));
            context.AddRange(brand, category, product, variant, offer, order);
            await context.SaveChangesAsync();
        }

        return new SeededPayment(order.Id, payment.Id, adminUserId, offer.Id, firstAttemptAt);
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

    private ServiceProvider CreateProvider(Guid? currentUserId, IPaymentGateway? gateway = null)
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
        services.Configure<OrderExpirationOptions>(options =>
        {
            options.MaxVerificationAttempts = 5;
            options.MaxVerificationAgeMinutes = 20;
        });

        if (gateway is not null)
            services.AddSingleton<IPaymentGateway>(gateway);

        return services.BuildServiceProvider();
    }

    private static async Task<ResolveAdminPaymentResponse> SendAsync(
        IServiceProvider provider,
        Guid paymentId,
        PaymentResolutionAction action,
        string reason,
        long? refId = null)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<MediatR.ISender>();
        return await sender.Send(new ResolveAdminPaymentCommand(paymentId, action, reason, refId));
    }

    private sealed record SeededPayment(
        Guid OrderId,
        Guid PaymentId,
        Guid AdminUserId,
        Guid SellerOfferId,
        DateTime FirstVerificationAttemptedAt);

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }

    private sealed class FixedZarinPalGateway(PaymentGatewayVerificationResult result) : IPaymentGateway
    {
        public int CallCount { get; private set; }

        public string GetPaymentUrl(string authority) =>
            $"https://www.zarinpal.com/pg/StartPay/{Uri.EscapeDataString(authority)}";

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
            return Task.FromResult(result);
        }
    }
}
