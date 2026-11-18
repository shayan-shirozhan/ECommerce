using ECommerce.API.BackgroundServices;
using ECommerce.Application;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Options;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Tests.API.BackgroundServices;

public sealed class ZarinPalPaymentReconciliationBackgroundServiceTests : PostgresTestBase
{
    [Test]
    public async Task ExecuteAsync_should_reconcile_due_payment_before_order_expiration_window()
    {
        var authority = $"A{Guid.NewGuid():N}";
        var seeded = await SeedPaymentAsync(authority, DateTime.UtcNow.AddMinutes(-4));
        var gateway = new SignallingZarinPalGateway(new PaymentGatewayVerificationResult(
            false, false, null, "503", "Provider unavailable.", IsIndeterminate: true));
        using var provider = CreateProvider(gateway);

        var service = new ZarinPalPaymentReconciliationBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new OrderExpirationOptions { ReconciliationIntervalSeconds = 3600 }),
            provider.GetRequiredService<ILogger<ZarinPalPaymentReconciliationBackgroundService>>());

        await service.StartAsync(CancellationToken.None);
        try
        {
            await gateway.Called.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await WaitForPersistedAttemptAsync(seeded.OrderId);
        }
        finally
        {
            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await service.StopAsync(stopTimeout.Token);
        }

        await using var context = CreateDbContext();
        var order = await context.Orders.Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);

        Assert.Multiple(() =>
        {
            Assert.That(order.CreatedAt, Is.GreaterThan(DateTime.UtcNow.AddMinutes(-30)));
            Assert.That(order.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(order.Payments.Single().Status, Is.EqualTo(PaymentStatus.Pending));
            Assert.That(order.Payments.Single().VerificationAttemptCount, Is.EqualTo(2));
            Assert.That(order.Payments.Single().LastVerificationErrorCode, Is.EqualTo("503"));
            Assert.That(gateway.CallCount, Is.EqualTo(1));
        });
    }

    private async Task WaitForPersistedAttemptAsync(Guid orderId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            await using var context = CreateDbContext();
            var state = await context.Orders
                .Where(order => order.Id == orderId)
                .SelectMany(order => order.Payments)
                .Select(payment => new { payment.VerificationAttemptCount, payment.LastVerificationErrorCode })
                .SingleAsync();

            if (state.VerificationAttemptCount == 2 && state.LastVerificationErrorCode == "503")
                return;

            await Task.Delay(50);
        }

        Assert.Fail("The reconciliation worker did not persist the verification result in time.");
    }

    private async Task<SeededPayment> SeedPaymentAsync(string authority, DateTime lastAttemptAtUtc)
    {
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var order = Order.Create(buyerId, "Test Buyer", "09120000000", "Tehran", "Tehran", "1234567890", "Test Street");
        order.AddItem(sellerId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Product", "Default", "Test Seller", 200m, 1, 0m);
        var payment = order.InitiatePayment(PaymentMethod.ZarinPal);
        payment.SetAuthority(authority);
        payment.MarkVerificationAttempted(lastAttemptAtUtc);

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(CreateUser(buyerId, $"buyer-{suffix}"), CreateUser(sellerId, $"seller-{suffix}"));
            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        return new SeededPayment(order.Id);
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

    private sealed record SeededPayment(Guid OrderId);

    private sealed class SignallingZarinPalGateway(PaymentGatewayVerificationResult result) : IPaymentGateway
    {
        public TaskCompletionSource<bool> Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CallCount { get; private set; }
        public string GetPaymentUrl(string authority) => "https://www.zarinpal.com/pg/StartPay/" + authority;
        public Task<PaymentGatewayRequestResult> RequestPaymentAsync(Guid paymentId, Guid orderId, decimal amount, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<PaymentGatewayVerificationResult> VerifyPaymentAsync(decimal amount, string authority, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            Called.TrySetResult(true);
            return Task.FromResult(result);
        }
    }
}
