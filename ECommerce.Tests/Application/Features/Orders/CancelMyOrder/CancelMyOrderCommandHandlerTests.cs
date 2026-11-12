using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Orders.CancelMyOrder;
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

namespace ECommerce.Tests.Application.Features.Orders.CancelMyOrder;

public sealed class CancelMyOrderCommandHandlerTests : PostgresTestBase
{
    [Test]
    public async Task Handle_should_cancel_pending_order_and_restore_stock_for_every_seller_group()
    {
        var seeded = await SeedOrderAsync();
        using var provider = CreateProvider(seeded.BuyerId);

        var response = await SendAsync(provider, seeded.OrderId);

        await using var verificationContext = CreateDbContext();
        var order = await verificationContext.Orders
            .Include(entity => entity.Groups)
            .ThenInclude(group => group.Items)
            .SingleAsync(entity => entity.Id == seeded.OrderId);
        var firstOffer = await verificationContext.SellerOffers
            .SingleAsync(offer => offer.Id == seeded.FirstOfferId);
        var secondOffer = await verificationContext.SellerOffers
            .SingleAsync(offer => offer.Id == seeded.SecondOfferId);

        Assert.Multiple(() =>
        {
            Assert.That(response.OrderId, Is.EqualTo(seeded.OrderId));
            Assert.That(response.OrderStatus, Is.EqualTo(OrderStatus.Cancelled));
            Assert.That(response.RestoredItemCount, Is.EqualTo(2));
            Assert.That(response.RestoredQuantity, Is.EqualTo(5));
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Cancelled));
            Assert.That(order.Groups.Select(group => group.Status),
                Is.All.EqualTo(OrderGroupStatus.Cancelled));
            Assert.That(firstOffer.Stock, Is.EqualTo(10));
            Assert.That(secondOffer.Stock, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task Handle_should_cancel_pending_payment_and_restore_stock_for_awaiting_payment_order()
    {
        var seeded = await SeedOrderAsync(initiatePayment: true);
        using var provider = CreateProvider(seeded.BuyerId);

        var response = await SendAsync(provider, seeded.OrderId);

        await using var verificationContext = CreateDbContext();
        var order = await verificationContext.Orders
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);
        var firstOffer = await verificationContext.SellerOffers
            .SingleAsync(offer => offer.Id == seeded.FirstOfferId);
        var secondOffer = await verificationContext.SellerOffers
            .SingleAsync(offer => offer.Id == seeded.SecondOfferId);

        Assert.Multiple(() =>
        {
            Assert.That(response.OrderStatus, Is.EqualTo(OrderStatus.Cancelled));
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Cancelled));
            Assert.That(order.Payments, Has.Count.EqualTo(1));
            Assert.That(order.Payments.Single().Status, Is.EqualTo(PaymentStatus.Cancelled));
            Assert.That(firstOffer.Stock, Is.EqualTo(10));
            Assert.That(secondOffer.Stock, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task Handle_should_restore_stock_even_when_seller_offer_is_inactive()
    {
        var seeded = await SeedOrderAsync();

        await using (var setupContext = CreateDbContext())
        {
            var offer = await setupContext.SellerOffers
                .SingleAsync(entity => entity.Id == seeded.FirstOfferId);
            offer.Deactivate();
            await setupContext.SaveChangesAsync();
        }

        using var provider = CreateProvider(seeded.BuyerId);

        await SendAsync(provider, seeded.OrderId);

        await using var verificationContext = CreateDbContext();
        var offerAfterCancellation = await verificationContext.SellerOffers
            .SingleAsync(entity => entity.Id == seeded.FirstOfferId);

        Assert.Multiple(() =>
        {
            Assert.That(offerAfterCancellation.Status, Is.EqualTo(SellerOfferStatus.Inactive));
            Assert.That(offerAfterCancellation.Stock, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task Handle_should_restore_inventory_only_once_when_cancellation_requests_are_concurrent()
    {
        var seeded = await SeedOrderAsync();
        using var provider = CreateProvider(seeded.BuyerId);

        var outcomes = await Task.WhenAll(
            CaptureFailureAsync(provider, seeded.OrderId),
            CaptureFailureAsync(provider, seeded.OrderId));

        await using var verificationContext = CreateDbContext();
        var order = await verificationContext.Orders.SingleAsync(entity => entity.Id == seeded.OrderId);
        var firstOffer = await verificationContext.SellerOffers
            .SingleAsync(offer => offer.Id == seeded.FirstOfferId);
        var secondOffer = await verificationContext.SellerOffers
            .SingleAsync(offer => offer.Id == seeded.SecondOfferId);

        Assert.Multiple(() =>
        {
            Assert.That(outcomes.Count(exception => exception is null), Is.EqualTo(1));
            Assert.That(outcomes.Count(exception => exception is not null), Is.EqualTo(1));
            Assert.That(outcomes.Where(exception => exception is not null).All(exception =>
                exception is DomainException or ConcurrencyConflictException), Is.True);
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Cancelled));
            Assert.That(firstOffer.Stock, Is.EqualTo(10));
            Assert.That(secondOffer.Stock, Is.EqualTo(10));
        });
    }

    [Test]
    public void Handle_should_require_an_authenticated_user()
    {
        using var provider = CreateProvider(null);

        Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(provider, Guid.NewGuid()));
    }

    [Test]
    public async Task Handle_should_not_cancel_another_users_order()
    {
        var seeded = await SeedOrderAsync();
        using var provider = CreateProvider(Guid.NewGuid());

        Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(provider, seeded.OrderId));

        await using var verificationContext = CreateDbContext();
        var order = await verificationContext.Orders.SingleAsync(entity => entity.Id == seeded.OrderId);
        var firstOffer = await verificationContext.SellerOffers
            .SingleAsync(offer => offer.Id == seeded.FirstOfferId);

        Assert.Multiple(() =>
        {
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Pending));
            Assert.That(firstOffer.Stock, Is.EqualTo(8));
        });
    }

    [Test]
    public async Task Handle_should_reject_confirmed_order_without_restoring_stock()
    {
        var seeded = await SeedOrderAsync(initiatePayment: true, confirmPayment: true);
        using var provider = CreateProvider(seeded.BuyerId);

        Assert.ThrowsAsync<DomainException>(
            async () => await SendAsync(provider, seeded.OrderId));

        await using var verificationContext = CreateDbContext();
        var order = await verificationContext.Orders
            .Include(entity => entity.Payments)
            .SingleAsync(entity => entity.Id == seeded.OrderId);
        var firstOffer = await verificationContext.SellerOffers
            .SingleAsync(offer => offer.Id == seeded.FirstOfferId);
        var secondOffer = await verificationContext.SellerOffers
            .SingleAsync(offer => offer.Id == seeded.SecondOfferId);

        Assert.Multiple(() =>
        {
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Confirmed));
            Assert.That(order.Payments.Single().Status, Is.EqualTo(PaymentStatus.Successful));
            Assert.That(firstOffer.Stock, Is.EqualTo(8));
            Assert.That(secondOffer.Stock, Is.EqualTo(7));
        });
    }

    [Test]
    public async Task Handle_should_not_persist_cancellation_when_a_seller_offer_is_missing()
    {
        var seeded = await SeedOrderAsync();

        await using (var setupContext = CreateDbContext())
        {
            var offer = await setupContext.SellerOffers
                .SingleAsync(entity => entity.Id == seeded.SecondOfferId);
            setupContext.SellerOffers.Remove(offer);
            await setupContext.SaveChangesAsync();
        }

        using var provider = CreateProvider(seeded.BuyerId);

        Assert.ThrowsAsync<BusinessRuleException>(
            async () => await SendAsync(provider, seeded.OrderId));

        await using var verificationContext = CreateDbContext();
        var order = await verificationContext.Orders.SingleAsync(entity => entity.Id == seeded.OrderId);
        var firstOffer = await verificationContext.SellerOffers
            .SingleAsync(offer => offer.Id == seeded.FirstOfferId);

        Assert.Multiple(() =>
        {
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Pending));
            Assert.That(firstOffer.Stock, Is.EqualTo(8));
        });
    }

    [Test]
    public void Validator_should_reject_empty_order_id()
    {
        var validator = new CancelMyOrderCommandValidator();

        var result = validator.Validate(new CancelMyOrderCommand(Guid.Empty));

        Assert.That(result.IsValid, Is.False);
    }

    private async Task<SeededOrder> SeedOrderAsync(
        bool initiatePayment = false,
        bool confirmPayment = false)
    {
        var buyerId = Guid.NewGuid();
        var firstSellerId = Guid.NewGuid();
        var secondSellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var brand = Brand.Create($"Cancellation Brand {suffix}");
        var category = Category.Create($"Cancellation Category {suffix}");
        var product = Product.Create(
            $"Cancellation Product {suffix}",
            "Product used by cancellation tests.",
            brand.Id,
            category.Id);
        product.Activate();

        var variant = ProductVariant.Create(product, $"CANCEL-{suffix}", "Default");
        variant.Activate(product);

        // Stock is already reduced because the order has reserved 2 and 3 units.
        var firstOffer = SellerOffer.Create(firstSellerId, variant.Id, 100m, stock: 8);
        var secondOffer = SellerOffer.Create(secondSellerId, variant.Id, 50m, stock: 7);
        firstOffer.Activate();
        secondOffer.Activate();

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
            product.Id,
            variant.Id,
            firstOffer.Id,
            product.Name,
            variant.Name ?? "Default",
            "First Seller",
            100m,
            2,
            0m);
        order.AddItem(
            secondSellerId,
            product.Id,
            variant.Id,
            secondOffer.Id,
            product.Name,
            variant.Name ?? "Default",
            "Second Seller",
            50m,
            3,
            0m);

        if (initiatePayment)
        {
            var payment = order.InitiatePayment();
            if (confirmPayment)
            {
                payment.MarkSuccessful($"cancel-test-txn-{Guid.NewGuid():N}");
                order.Confirm();
            }
        }

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(buyerId, $"buyer-{suffix}", "Test", "Buyer"),
                CreateUser(firstSellerId, $"seller-a-{suffix}", "First", "Seller"),
                CreateUser(secondSellerId, $"seller-b-{suffix}", "Second", "Seller"));
            context.AddRange(brand, category, product, variant, firstOffer, secondOffer, order);
            await context.SaveChangesAsync();
        }

        return new SeededOrder(
            buyerId,
            order.Id,
            firstOffer.Id,
            secondOffer.Id);
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
        services.AddScoped<ICurrentUser>(_ => new TestCurrentUser(currentUserId));

        return services.BuildServiceProvider();
    }

    private static async Task<Exception?> CaptureFailureAsync(
        IServiceProvider provider,
        Guid orderId)
    {
        try
        {
            await SendAsync(provider, orderId);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static async Task<CancelMyOrderResponse> SendAsync(
        IServiceProvider provider,
        Guid orderId)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new CancelMyOrderCommand(orderId));
    }

    private sealed record SeededOrder(
        Guid BuyerId,
        Guid OrderId,
        Guid FirstOfferId,
        Guid SecondOfferId);

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }
}
