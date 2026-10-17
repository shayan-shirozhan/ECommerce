using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Orders.CreateOrderFromCart;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Orders.CreateOrderFromCart;

public sealed class CreateOrderFromCartCommandHandlerTests : PostgresTestBase
{
    [Test]
    public async Task Handle_should_create_order_decrease_stock_and_clear_cart()
    {
        var scenario = await SeedScenarioAsync(stock: 10, quantity: 2);
        using var provider = CreateProvider(scenario.BuyerId);

        var response = await SendAsync(provider, ValidCommand());

        Assert.That(response.OrderId, Is.Not.EqualTo(Guid.Empty));
        Assert.That(response.Status, Is.EqualTo(OrderStatus.Pending));
        Assert.That(response.TotalAmount, Is.EqualTo(200m));
        Assert.That(response.GroupCount, Is.EqualTo(1));
        Assert.That(response.ItemCount, Is.EqualTo(1));

        await using var verificationContext = CreateDbContext();
        var order = await verificationContext.Orders
            .Include(entity => entity.Groups)
            .ThenInclude(group => group.Items)
            .SingleAsync(entity => entity.Id == response.OrderId);

        var group = order.Groups.Single();
        var item = group.Items.Single();
        Assert.That(order.UserId, Is.EqualTo(scenario.BuyerId));
        Assert.That(order.ShippingRecipientName, Is.EqualTo("Buyer Test"));
        Assert.That(group.SellerId, Is.EqualTo(scenario.SellerId));
        Assert.That(item.ProductName, Is.EqualTo(scenario.ProductName));
        Assert.That(item.VariantName, Is.EqualTo("Default"));
        Assert.That(item.SellerName, Is.EqualTo("Test Seller"));
        Assert.That(item.UnitPrice, Is.EqualTo(100m));
        Assert.That(item.Quantity, Is.EqualTo(2));
        Assert.That(item.DiscountAmount, Is.EqualTo(0m));
        Assert.That(item.TotalAmount, Is.EqualTo(200m));

        var savedCart = await verificationContext.Carts
            .Include(entity => entity.Items)
            .SingleAsync(entity => entity.UserId == scenario.BuyerId);
        Assert.That(savedCart.Items, Is.Empty);

        var savedOffer = await verificationContext.SellerOffers
            .SingleAsync(entity => entity.Id == scenario.OfferId);
        Assert.That(savedOffer.Stock, Is.EqualTo(8));
    }

    [Test]
    public async Task Handle_should_reject_empty_cart_without_creating_order()
    {
        var scenario = await SeedScenarioAsync(stock: 10, quantity: 0);
        using var provider = CreateProvider(scenario.BuyerId);

        Assert.ThrowsAsync<BusinessRuleException>(
            async () => await SendAsync(provider, ValidCommand()));

        await using var verificationContext = CreateDbContext();
        Assert.That(await verificationContext.Orders.AnyAsync(order => order.UserId == scenario.BuyerId), Is.False);
        var savedCart = await verificationContext.Carts
            .Include(entity => entity.Items)
            .SingleAsync(entity => entity.UserId == scenario.BuyerId);
        Assert.That(savedCart.Items, Is.Empty);
    }

    [Test]
    public async Task Handle_should_reject_quantity_above_stock_and_preserve_cart()
    {
        var scenario = await SeedScenarioAsync(stock: 2, quantity: 3);
        using var provider = CreateProvider(scenario.BuyerId);

        Assert.ThrowsAsync<BusinessRuleException>(
            async () => await SendAsync(provider, ValidCommand()));

        await using var verificationContext = CreateDbContext();
        Assert.That(await verificationContext.Orders.AnyAsync(order => order.UserId == scenario.BuyerId), Is.False);

        var savedCart = await verificationContext.Carts
            .Include(entity => entity.Items)
            .SingleAsync(entity => entity.UserId == scenario.BuyerId);
        Assert.That(savedCart.Items.Single().Quantity, Is.EqualTo(3));

        var savedOffer = await verificationContext.SellerOffers
            .SingleAsync(entity => entity.Id == scenario.OfferId);
        Assert.That(savedOffer.Stock, Is.EqualTo(2));
    }

    [Test]
    public async Task Handle_should_reject_inactive_offer_and_preserve_cart()
    {
        var scenario = await SeedScenarioAsync(stock: 10, quantity: 1, activateOffer: false);
        using var provider = CreateProvider(scenario.BuyerId);

        Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(provider, ValidCommand()));

        await using var verificationContext = CreateDbContext();
        Assert.That(await verificationContext.Orders.AnyAsync(order => order.UserId == scenario.BuyerId), Is.False);
        var savedCart = await verificationContext.Carts
            .Include(entity => entity.Items)
            .SingleAsync(entity => entity.UserId == scenario.BuyerId);
        Assert.That(savedCart.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public void Handle_should_require_authenticated_user()
    {
        using var provider = CreateProvider(currentUserId: null);

        Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(provider, ValidCommand()));
    }

    private async Task<SeededScenario> SeedScenarioAsync(
        int stock,
        int quantity,
        bool activateOffer = true)
    {
        var buyerId = Guid.NewGuid();
        var sellerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var buyerName = $"buyer-{suffix}";
        var sellerName = $"seller-{suffix}";

        await using var context = CreateDbContext();

        var buyer = CreateUser(buyerId, buyerName, "Buyer", "Test");
        var seller = CreateUser(sellerId, sellerName, "Test", "Seller");
        var brand = Brand.Create($"Checkout Brand {suffix}");
        var category = Category.Create($"Checkout Category {suffix}");
        var product = Product.Create(
            $"Checkout Product {suffix}",
            "Product used by checkout tests.",
            brand.Id,
            category.Id);
        product.Activate();

        var variant = ProductVariant.Create(product, $"CHECKOUT-{suffix}", "Default");
        variant.Activate(product);

        var offer = SellerOffer.Create(sellerId, variant.Id, 100m, stock);
        if (activateOffer)
            offer.Activate();

        var cart = ECommerce.Domain.Entities.Cart.Create(buyerId);
        if (quantity > 0)
            cart.AddItem(offer.Id, quantity);

        context.Users.AddRange(buyer, seller);
        context.AddRange(brand, category, product, variant, offer, cart);
        await context.SaveChangesAsync();

        return new SeededScenario(buyerId, sellerId, offer.Id, product.Name);
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

    private static CreateOrderFromCartCommand ValidCommand() => new(
        ShippingRecipientName: "Buyer Test",
        ShippingPhoneNumber: "09120000000",
        ShippingProvince: "Tehran",
        ShippingCity: "Tehran",
        ShippingPostalCode: "1234567890",
        ShippingAddressLine: "Test Street 1");

    private static async Task<CreateOrderFromCartResponse> SendAsync(
        IServiceProvider provider,
        CreateOrderFromCartCommand command)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(command);
    }

    private sealed record SeededScenario(
        Guid BuyerId,
        Guid SellerId,
        Guid OfferId,
        string ProductName);

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }
}
