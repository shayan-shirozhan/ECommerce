using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Cart.AddCartItem;
using ECommerce.Application.Features.Cart.UpdateCartItem;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShoppingCart = ECommerce.Domain.Entities.Cart;

namespace ECommerce.Tests.Application.Features.Cart.UpdateCartItem;

public sealed class UpdateCartItemCommandHandlerTests : PostgresTestBase
{
    [Test]
    public async Task Handle_should_replace_quantity_for_existing_cart_item()
    {
        var (userId, offerId) = await SeedOfferAsync(stock: 10);
        using var provider = CreateProvider(userId);
        await AddItemAsync(provider, offerId, 2);

        var response = await UpdateItemAsync(provider, new UpdateCartItemCommand(offerId, 5));

        Assert.Multiple(() =>
        {
            Assert.That(response.SellerOfferId, Is.EqualTo(offerId));
            Assert.That(response.Quantity, Is.EqualTo(5));
        });

        await using var verificationContext = CreateDbContext();
        var cart = await verificationContext.Carts
            .Include(entity => entity.Items)
            .SingleAsync(entity => entity.UserId == userId);
        Assert.That(cart.Items.Single().Quantity, Is.EqualTo(5));
    }

    [Test]
    public async Task Handle_should_reject_quantity_greater_than_stock_without_changing_cart()
    {
        var (userId, offerId) = await SeedOfferAsync(stock: 3);
        using var provider = CreateProvider(userId);
        await AddItemAsync(provider, offerId, 2);

        Assert.ThrowsAsync<BusinessRuleException>(
            async () => await UpdateItemAsync(provider, new UpdateCartItemCommand(offerId, 4)));

        await using var verificationContext = CreateDbContext();
        var cart = await verificationContext.Carts
            .Include(entity => entity.Items)
            .SingleAsync(entity => entity.UserId == userId);
        Assert.That(cart.Items.Single().Quantity, Is.EqualTo(2));
    }

    [Test]
    public async Task Handle_should_not_update_another_users_cart_item()
    {
        var (currentUserId, offerId) = await SeedOfferAsync(stock: 10);
        var otherUserId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        await using (var context = CreateDbContext())
        {
            context.Users.Add(CreateUser(otherUserId, $"other-{suffix}"));
            var otherCart = ShoppingCart.Create(otherUserId);
            otherCart.AddItem(offerId, 2);
            context.Carts.Add(otherCart);
            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(currentUserId);
        Assert.ThrowsAsync<NotFoundException>(
            async () => await UpdateItemAsync(provider, new UpdateCartItemCommand(offerId, 5)));

        await using var verificationContext = CreateDbContext();
        var otherCartAfterRequest = await verificationContext.Carts
            .Include(entity => entity.Items)
            .SingleAsync(entity => entity.UserId == otherUserId);
        Assert.That(otherCartAfterRequest.Items.Single().Quantity, Is.EqualTo(2));
    }

    [Test]
    public async Task Handle_should_reject_offer_that_is_not_in_current_users_cart()
    {
        var (userId, existingOfferId) = await SeedOfferAsync(stock: 10);
        var (_, otherOfferId) = await SeedOfferAsync(stock: 10);
        using var provider = CreateProvider(userId);
        await AddItemAsync(provider, existingOfferId, 2);

        Assert.ThrowsAsync<NotFoundException>(
            async () => await UpdateItemAsync(provider, new UpdateCartItemCommand(otherOfferId, 5)));

        await using var verificationContext = CreateDbContext();
        var cart = await verificationContext.Carts
            .Include(entity => entity.Items)
            .SingleAsync(entity => entity.UserId == userId);
        Assert.That(cart.Items, Has.Count.EqualTo(1));
        Assert.That(cart.Items.Single().SellerOfferId, Is.EqualTo(existingOfferId));
        Assert.That(cart.Items.Single().Quantity, Is.EqualTo(2));
    }

    [Test]
    public async Task Handle_should_reject_inactive_seller_offer()
    {
        var (userId, offerId) = await SeedOfferAsync(stock: 10, activateOffer: false);
        await using (var context = CreateDbContext())
        {
            var cart = ShoppingCart.Create(userId);
            cart.AddItem(offerId, 1);
            context.Carts.Add(cart);
            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(userId);
        Assert.ThrowsAsync<NotFoundException>(
            async () => await UpdateItemAsync(provider, new UpdateCartItemCommand(offerId, 2)));
    }

    [Test]
    public void Handle_should_reject_request_without_authenticated_user_id()
    {
        using var provider = CreateProvider(currentUserId: null);

        Assert.ThrowsAsync<UnauthorizedException>(
            async () => await UpdateItemAsync(
                provider,
                new UpdateCartItemCommand(Guid.NewGuid(), 1)));
    }

    private async Task<(Guid UserId, Guid OfferId)> SeedOfferAsync(
        int stock,
        bool activateOffer = true)
    {
        var userId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var userName = $"update-cart-test-{suffix}";

        await using var context = CreateDbContext();
        context.Users.Add(CreateUser(userId, userName));

        var brand = Brand.Create($"Update Cart Test Brand {suffix}");
        var category = Category.Create($"Update Cart Test Category {suffix}");
        var product = Product.Create(
            $"Update Cart Test Product {suffix}",
            "Test product description.",
            brand.Id,
            category.Id);
        product.Activate();

        var variant = ProductVariant.Create(product, $"UPDATE-CART-{suffix}", "Default");
        variant.Activate(product);

        var offer = SellerOffer.Create(userId, variant.Id, 100m, stock);
        if (activateOffer)
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

    private static async Task<AddCartItemResponse> AddItemAsync(
        IServiceProvider provider,
        Guid sellerOfferId,
        int quantity)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new AddCartItemCommand(sellerOfferId, quantity));
    }

    private static async Task<UpdateCartItemResponse> UpdateItemAsync(
        IServiceProvider provider,
        UpdateCartItemCommand command)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(command);
    }

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }
}
