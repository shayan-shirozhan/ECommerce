using ECommerce.Application;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Payments.CompleteZarinPalPayment;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Payments.CompleteZarinPalPayment;

public sealed class CompleteZarinPalPaymentCommandHandlerTests : PostgresTestBase
{
    [Test]
    public async Task Handle_should_verify_payment_and_confirm_order()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedZarinPalPaymentAsync(authority);
        var gateway = new FixedZarinPalGateway(
            new PaymentGatewayVerificationResult(true, false, 123456789));
        using var provider = CreateProvider(gateway);

        var response = await SendAsync(provider, seeded.PaymentId, authority, "OK");

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(order => order.Payments)
            .SingleAsync(order => order.Id == seeded.OrderId);
        var payment = persistedOrder.Payments.Single();

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccessful, Is.True);
            Assert.That(response.WasAlreadyProcessed, Is.False);
            Assert.That(response.PaymentStatus, Is.EqualTo(PaymentStatus.Successful));
            Assert.That(response.OrderStatus, Is.EqualTo(OrderStatus.Confirmed));
            Assert.That(response.RefId, Is.EqualTo(123456789));
            Assert.That(payment.RefId, Is.EqualTo(123456789));
            Assert.That(payment.Authority, Is.EqualTo(authority));
            Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Successful));
            Assert.That(persistedOrder.Status, Is.EqualTo(OrderStatus.Confirmed));
            Assert.That(gateway.CallCount, Is.EqualTo(1));
            Assert.That(gateway.LastAmount, Is.EqualTo(200m));
            Assert.That(gateway.LastAuthority, Is.EqualTo(authority));
        });
    }

    [Test]
    public async Task Handle_should_mark_payment_failed_without_verifying_when_callback_status_is_nok()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedZarinPalPaymentAsync(authority);
        var gateway = new FixedZarinPalGateway(
            new PaymentGatewayVerificationResult(false, false, null, "-51", "Payment not completed"));
        using var provider = CreateProvider(gateway);

        var response = await SendAsync(provider, seeded.PaymentId, authority, "NOK");

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(order => order.Payments)
            .SingleAsync(order => order.Id == seeded.OrderId);
        var payment = persistedOrder.Payments.Single();

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccessful, Is.False);
            Assert.That(response.PaymentStatus, Is.EqualTo(PaymentStatus.Failed));
            Assert.That(response.OrderStatus, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Failed));
            Assert.That(persistedOrder.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(gateway.CallCount, Is.Zero);
        });
    }

    [Test]
    public async Task Handle_should_mark_ok_callback_failed_when_provider_verification_fails()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedZarinPalPaymentAsync(authority);
        var gateway = new FixedZarinPalGateway(
            new PaymentGatewayVerificationResult(false, false, null, "-51", "Payment not completed"));
        using var provider = CreateProvider(gateway);

        var response = await SendAsync(provider, seeded.PaymentId, authority, "OK");

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(order => order.Payments)
            .SingleAsync(order => order.Id == seeded.OrderId);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccessful, Is.False);
            Assert.That(response.PaymentStatus, Is.EqualTo(PaymentStatus.Failed));
            Assert.That(response.OrderStatus, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(persistedOrder.Payments.Single().Status, Is.EqualTo(PaymentStatus.Failed));
            Assert.That(gateway.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Handle_should_keep_payment_pending_when_verification_outcome_is_indeterminate()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedZarinPalPaymentAsync(authority);
        var gateway = new FixedZarinPalGateway(
            new PaymentGatewayVerificationResult(
                false,
                false,
                null,
                "503",
                "Provider temporarily unavailable.",
                IsIndeterminate: true));
        using var provider = CreateProvider(gateway);

        Assert.ThrowsAsync<PaymentGatewayException>(
            async () => await SendAsync(provider, seeded.PaymentId, authority, "OK"));

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(order => order.Payments)
            .SingleAsync(order => order.Id == seeded.OrderId);

        Assert.Multiple(() =>
        {
            Assert.That(persistedOrder.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(persistedOrder.Payments.Single().Status, Is.EqualTo(PaymentStatus.Pending));
            Assert.That(persistedOrder.Payments.Single().RefId, Is.Null);
            Assert.That(persistedOrder.Payments.Single().LastVerificationAttemptedAt, Is.Not.Null);
            Assert.That(gateway.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Handle_should_not_repeat_verification_while_previous_attempt_is_within_retry_delay()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedZarinPalPaymentAsync(
            authority,
            DateTime.UtcNow.AddSeconds(-30));
        var gateway = new FixedZarinPalGateway(
            new PaymentGatewayVerificationResult(true, false, 555123));
        using var provider = CreateProvider(gateway);

        Assert.ThrowsAsync<BusinessRuleException>(
            async () => await SendAsync(provider, seeded.PaymentId, authority, "OK"));

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
    public async Task Handle_should_accept_provider_code_101_as_an_idempotent_verified_payment()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedZarinPalPaymentAsync(authority);
        var gateway = new FixedZarinPalGateway(
            new PaymentGatewayVerificationResult(true, true, 987654321));
        using var provider = CreateProvider(gateway);

        var response = await SendAsync(provider, seeded.PaymentId, authority, "OK");

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccessful, Is.True);
            Assert.That(response.RefId, Is.EqualTo(987654321));
            Assert.That(gateway.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Handle_should_not_call_provider_again_for_a_locally_completed_callback()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedZarinPalPaymentAsync(authority);
        var gateway = new FixedZarinPalGateway(
            new PaymentGatewayVerificationResult(true, false, 555123));
        using var provider = CreateProvider(gateway);

        await SendAsync(provider, seeded.PaymentId, authority, "OK");
        var duplicateResponse = await SendAsync(provider, seeded.PaymentId, authority, "OK");

        Assert.Multiple(() =>
        {
            Assert.That(duplicateResponse.IsSuccessful, Is.True);
            Assert.That(duplicateResponse.WasAlreadyProcessed, Is.True);
            Assert.That(gateway.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Handle_should_reject_callback_when_authority_does_not_match_payment()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedZarinPalPaymentAsync(authority);
        var gateway = new FixedZarinPalGateway(
            new PaymentGatewayVerificationResult(true, false, 555123));
        using var provider = CreateProvider(gateway);

        Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(provider, seeded.PaymentId, "A-different-authority", "OK"));

        Assert.That(gateway.CallCount, Is.Zero);
    }

    [Test]
    public async Task Handle_should_reject_invalid_callback_status_without_contacting_provider()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedZarinPalPaymentAsync(authority);
        var gateway = new FixedZarinPalGateway(
            new PaymentGatewayVerificationResult(true, false, 555123));
        using var provider = CreateProvider(gateway);

        Assert.ThrowsAsync<BusinessRuleException>(
            async () => await SendAsync(provider, seeded.PaymentId, authority, "UNKNOWN"));

        Assert.That(gateway.CallCount, Is.Zero);
    }


    [Test]
    public async Task Handle_should_keep_payment_pending_when_gateway_is_not_configured_for_ok_callback()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedZarinPalPaymentAsync(authority);
        using var provider = CreateProvider();

        Assert.ThrowsAsync<PaymentGatewayException>(
            async () => await SendAsync(provider, seeded.PaymentId, authority, "OK"));

        await using var verificationContext = CreateDbContext();
        var persistedOrder = await verificationContext.Orders
            .Include(order => order.Payments)
            .SingleAsync(order => order.Id == seeded.OrderId);

        Assert.Multiple(() =>
        {
            Assert.That(persistedOrder.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(persistedOrder.Payments.Single().Status, Is.EqualTo(PaymentStatus.Pending));
        });
    }

    private async Task<SeededPayment> SeedZarinPalPaymentAsync(
        string authority,
        DateTime? lastVerificationAttemptedAt = null)
    {
        var userId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var order = CreateOrderWithItem(userId, sellerId);
        var payment = order.InitiatePayment(PaymentMethod.ZarinPal);
        payment.SetAuthority(authority);
        if (lastVerificationAttemptedAt.HasValue)
            payment.MarkVerificationAttempted(lastVerificationAttemptedAt.Value);

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(userId, $"buyer-{suffix}"),
                CreateUser(sellerId, $"seller-{suffix}"));
            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        return new SeededPayment(order.Id, payment.Id);
    }

    private ServiceProvider CreateProvider(IPaymentGateway? gateway = null)
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

        if (gateway is not null)
            services.AddScoped<IPaymentGateway>(_ => gateway);

        return services.BuildServiceProvider();
    }

    private static async Task<CompleteZarinPalPaymentResponse> SendAsync(
        IServiceProvider provider,
        Guid paymentId,
        string authority,
        string status)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(
            new CompleteZarinPalPaymentCommand(paymentId, authority, status));
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

    private sealed record SeededPayment(Guid OrderId, Guid PaymentId);

    private sealed class FixedZarinPalGateway(PaymentGatewayVerificationResult result)
        : IPaymentGateway
    {
        public int CallCount { get; private set; }
        public string? LastAuthority { get; private set; }
        public decimal LastAmount { get; private set; }

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
            LastAmount = amount;
            LastAuthority = authority;
            return Task.FromResult(result);
        }
    }
}
