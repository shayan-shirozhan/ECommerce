using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Cart.AddCartItem;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MediatR;

namespace ECommerce.Tests.Application.Features.Cart.AddCartItem;

public sealed class AddCartItemCommandHandlerTests : PostgresTestBase
{
    [Test]
    public async Task Handle_should_create_cart_and_add_item_for_current_user()
    {
        var (userId, offerId) = await SeedOfferAsync(stock: 10);
        using var provider = CreateProvider(userId);

        var response = await SendAsync(provider, new AddCartItemCommand(offerId, 2));

        Assert.That(response.SellerOfferId, Is.EqualTo(offerId));
        Assert.That(response.Quantity, Is.EqualTo(2));

        await using var verificationContext = CreateDbContext();
        var cart = await verificationContext.Carts
            .Include(entity => entity.Items)
            .SingleAsync(entity => entity.UserId == userId);

        Assert.That(cart.Items, Has.Count.EqualTo(1));
        Assert.That(cart.Items.Single().Quantity, Is.EqualTo(2));
    }

    [Test]
    public async Task Handle_should_increase_quantity_when_offer_already_exists_in_cart()
    {
        var (userId, offerId) = await SeedOfferAsync(stock: 10);
        using var provider = CreateProvider(userId);

        await SendAsync(provider, new AddCartItemCommand(offerId, 2));
        var response = await SendAsync(provider, new AddCartItemCommand(offerId, 3));

        Assert.That(response.Quantity, Is.EqualTo(5));

        await using var verificationContext = CreateDbContext();
        var cart = await verificationContext.Carts
            .Include(entity => entity.Items)
            .SingleAsync(entity => entity.UserId == userId);

        Assert.That(cart.Items, Has.Count.EqualTo(1));
        Assert.That(cart.Items.Single().Quantity, Is.EqualTo(5));
    }

    [Test]
    public async Task Handle_should_reject_quantity_greater_than_available_stock()
    {
        var (userId, offerId) = await SeedOfferAsync(stock: 3);
        using var provider = CreateProvider(userId);

        Assert.ThrowsAsync<BusinessRuleException>(
            async () => await SendAsync(provider, new AddCartItemCommand(offerId, 4)));

        await using var verificationContext = CreateDbContext();
        Assert.That(await verificationContext.Carts.AnyAsync(cart => cart.UserId == userId), Is.False);
    }

    [Test]
    public async Task Handle_should_reject_inactive_offer()
    {
        var (userId, offerId) = await SeedOfferAsync(stock: 10, activateOffer: false);
        using var provider = CreateProvider(userId);

        Assert.ThrowsAsync<NotFoundException>(
            async () => await SendAsync(provider, new AddCartItemCommand(offerId, 1)));
    }

    [Test]
    public async Task Handle_should_reject_request_without_authenticated_user_id()
    {
        using var provider = CreateProvider(currentUserId: null);

        Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(provider, new AddCartItemCommand(Guid.NewGuid(), 1)));
    }

    private async Task<(Guid UserId, Guid OfferId)> SeedOfferAsync(int stock, bool activateOffer = true)
    {
        var userId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var userName = $"cart-test-{suffix}";

        await using var context = CreateDbContext();
        var user = new ApplicationUser
        {
            Id = userId,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = $"{userName}@example.test",
            NormalizedEmail = $"{userName}@example.test".ToUpperInvariant(),
            FirstName = "Cart",
            LastName = "Test"
        };

        var brand = Brand.Create($"Cart Test Brand {suffix}");
        var category = Category.Create($"Cart Test Category {suffix}");
        var product = Product.Create($"Cart Test Product {suffix}", "Test product description.", brand.Id, category.Id);
        product.Activate();

        var variant = ProductVariant.Create(product, $"CART-{suffix}", "Default");
        variant.Activate(product);

        var offer = SellerOffer.Create(userId, variant.Id, 100m, stock);
        if (activateOffer)
            offer.Activate();

        context.Users.Add(user);
        context.AddRange(brand, category, product, variant, offer);
        await context.SaveChangesAsync();

        return (userId, offer.Id);
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

    private static async Task<AddCartItemResponse> SendAsync(
        IServiceProvider provider,
        AddCartItemCommand command)
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
