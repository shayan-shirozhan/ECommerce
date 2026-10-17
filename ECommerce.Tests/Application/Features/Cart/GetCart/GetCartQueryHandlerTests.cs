using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Cart.AddCartItem;
using ECommerce.Application.Features.Cart.GetCart;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MediatR;

namespace ECommerce.Tests.Application.Features.Cart.GetCart;

public sealed class GetCartQueryHandlerTests : PostgresTestBase
{
    [Test]
    public async Task Handle_should_return_empty_cart_without_creating_one_when_user_has_no_cart()
    {
        var (userId, _) = await SeedOfferAsync();
        using var provider = CreateProvider(userId);

        var response = await GetCartAsync(provider);

        Assert.That(response.Items, Is.Empty);
        Assert.That(response.TotalAmount, Is.EqualTo(0m));

        await using var verificationContext = CreateDbContext();
        Assert.That(
            await verificationContext.Carts.AnyAsync(cart => cart.UserId == userId),
            Is.False);
    }

    [Test]
    public async Task Handle_should_return_cart_items_with_current_offer_price_and_totals()
    {
        var (userId, offerId) = await SeedOfferAsync();
        using var provider = CreateProvider(userId);

        await AddItemAsync(provider, offerId, 2);
        var response = await GetCartAsync(provider);

        Assert.That(response.Items, Has.Count.EqualTo(1));
        var item = response.Items.Single();
        Assert.That(item.SellerOfferId, Is.EqualTo(offerId));
        Assert.That(item.ProductName, Does.StartWith("Cart Test Product "));
        Assert.That(item.VariantName, Is.EqualTo("Default"));
        Assert.That(item.Quantity, Is.EqualTo(2));
        Assert.That(item.UnitPrice, Is.EqualTo(100m));
        Assert.That(item.LineTotal, Is.EqualTo(200m));
        Assert.That(response.TotalAmount, Is.EqualTo(200m));
    }

    [Test]
    public async Task Handle_should_only_return_the_current_users_cart()
    {
        var (_, offerId) = await SeedOfferAsync();
        var currentUserId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(currentUserId, $"current-{suffix}"),
                CreateUser(anotherUserId, $"other-{suffix}"));

            var currentCart = ECommerce.Domain.Entities.Cart.Create(currentUserId);
            currentCart.AddItem(offerId, 1);

            var otherCart = ECommerce.Domain.Entities.Cart.Create(anotherUserId);
            otherCart.AddItem(offerId, 5);

            context.Carts.AddRange(currentCart, otherCart);
            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(currentUserId);
        var response = await GetCartAsync(provider);

        Assert.That(response.Items, Has.Count.EqualTo(1));
        Assert.That(response.Items.Single().Quantity, Is.EqualTo(1));
        Assert.That(response.TotalAmount, Is.EqualTo(100m));
    }

    [Test]
    public void Handle_should_reject_request_without_authenticated_user_id()
    {
        using var provider = CreateProvider(currentUserId: null);

        Assert.ThrowsAsync<UnauthorizedException>(async () => await GetCartAsync(provider));
    }

    private async Task<(Guid UserId, Guid OfferId)> SeedOfferAsync()
    {
        var userId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var userName = $"cart-query-test-{suffix}";

        await using var context = CreateDbContext();
        context.Users.Add(CreateUser(userId, userName));

        var brand = Brand.Create($"Cart Test Brand {suffix}");
        var category = Category.Create($"Cart Test Category {suffix}");
        var product = Product.Create(
            $"Cart Test Product {suffix}",
            "Test product description.",
            brand.Id,
            category.Id);
        product.Activate();

        var variant = ProductVariant.Create(product, $"CART-{suffix}", "Default");
        variant.Activate(product);

        var offer = SellerOffer.Create(userId, variant.Id, 100m, 10);
        offer.Activate();

        context.AddRange(brand, category, product, variant, offer);
        await context.SaveChangesAsync();

        return (userId, offer.Id);
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
            FirstName = "Cart",
            LastName = "Test"
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

    private static async Task AddItemAsync(
        IServiceProvider provider,
        Guid sellerOfferId,
        int quantity)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new AddCartItemCommand(sellerOfferId, quantity));
    }

    private static async Task<GetCartResponse> GetCartAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new GetCartQuery());
    }

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }
}
