using ECommerce.Application;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Orders.ExpireOrders;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
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

    private ServiceProvider CreateProvider()
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

    private sealed record SeededOrder(
        Guid OrderId,
        Guid OfferId,
        Guid? FailedPaymentId,
        Guid? PendingPaymentId);

    private sealed record ExpirationOutcome(
        bool? Processed,
        Exception? Exception);
}
