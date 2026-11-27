using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Wishlist.AddWishlistItem;
using ECommerce.Application.Features.Wishlist.GetMyWishlist;
using ECommerce.Application.Features.Wishlist.RemoveWishlistItem;
using ECommerce.Domain.Entities;
using UserWishlist = ECommerce.Domain.Entities.Wishlist;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Wishlist;

public sealed class WishlistHandlerTests : PostgresTestBase
{
    [Test]
    public async Task Add_should_create_wishlist_and_add_an_active_product()
    {
        var (userId, productId, _) = await SeedProductAsync();
        using var provider = CreateProvider(userId);

        var response = await AddAsync(provider, productId);

        Assert.Multiple(() =>
        {
            Assert.That(response.WishlistId, Is.Not.EqualTo(Guid.Empty));
            Assert.That(response.ProductId, Is.EqualTo(productId));
            Assert.That(response.Added, Is.True);
        });

        await using var context = CreateDbContext();
        var wishlist = await context.Wishlists
            .Include(entity => entity.Items)
            .SingleAsync(entity => entity.UserId == userId);
        Assert.That(wishlist.Items.Select(item => item.ProductId), Is.EqualTo(new[] { productId }));
    }

    [Test]
    public async Task Add_should_add_a_different_product_to_an_existing_wishlist()
    {
        var (userId, firstProductId, _) = await SeedProductAsync();
        var (_, secondProductId, _) = await SeedProductAsync();
        using var provider = CreateProvider(userId);

        var firstResponse = await AddAsync(provider, firstProductId);
        var secondResponse = await AddAsync(provider, secondProductId);

        Assert.Multiple(() =>
        {
            Assert.That(firstResponse.Added, Is.True);
            Assert.That(secondResponse.Added, Is.True);
            Assert.That(secondResponse.ProductId, Is.EqualTo(secondProductId));
        });

        await using var context = CreateDbContext();
        var wishlist = await context.Wishlists
            .Include(entity => entity.Items)
            .SingleAsync(entity => entity.UserId == userId);

        Assert.That(
            wishlist.Items.Select(item => item.ProductId),
            Is.EquivalentTo(new[] { firstProductId, secondProductId }));
    }

    [Test]
    public async Task Add_should_be_idempotent_when_product_already_exists()
    {
        var (userId, productId, _) = await SeedProductAsync();
        using var provider = CreateProvider(userId);

        var firstResponse = await AddAsync(provider, productId);
        var secondResponse = await AddAsync(provider, productId);

        Assert.Multiple(() =>
        {
            Assert.That(firstResponse.Added, Is.True);
            Assert.That(secondResponse.Added, Is.False);
            Assert.That(secondResponse.WishlistId, Is.EqualTo(firstResponse.WishlistId));
        });

        await using var context = CreateDbContext();
        var wishlist = await context.Wishlists
            .Include(entity => entity.Items)
            .SingleAsync(entity => entity.UserId == userId);
        Assert.That(wishlist.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Add_should_reject_a_missing_product()
    {
        var userId = await SeedUserAsync("wishlist-missing");
        using var provider = CreateProvider(userId);

        Assert.ThrowsAsync<NotFoundException>(
            async () => await AddAsync(provider, Guid.NewGuid()));

        await using var context = CreateDbContext();
        Assert.That(await context.Wishlists.AnyAsync(wishlist => wishlist.UserId == userId), Is.False);
    }

    [Test]
    public async Task Add_should_reject_a_product_that_is_not_active()
    {
        var (userId, productId, _) = await SeedProductAsync(activateProduct: false);
        using var provider = CreateProvider(userId);

        Assert.ThrowsAsync<NotFoundException>(
            async () => await AddAsync(provider, productId));

        await using var context = CreateDbContext();
        Assert.That(await context.Wishlists.AnyAsync(wishlist => wishlist.UserId == userId), Is.False);
    }

    [Test]
    public void Add_should_reject_a_request_without_an_authenticated_user()
    {
        using var provider = CreateProvider(currentUserId: null);

        Assert.ThrowsAsync<UnauthorizedException>(
            async () => await AddAsync(provider, Guid.NewGuid()));
    }

    [Test]
    public async Task Remove_should_remove_a_product_from_current_users_wishlist()
    {
        var (userId, productId, _) = await SeedProductAsync();
        using var provider = CreateProvider(userId);
        await AddAsync(provider, productId);

        await RemoveAsync(provider, productId);

        await using var context = CreateDbContext();
        var wishlist = await context.Wishlists
            .Include(entity => entity.Items)
            .SingleAsync(entity => entity.UserId == userId);
        Assert.That(wishlist.Items, Is.Empty);
    }

    [Test]
    public async Task Remove_should_be_idempotent_when_product_or_wishlist_is_missing()
    {
        var userId = await SeedUserAsync("wishlist-remove-empty");
        using var provider = CreateProvider(userId);

        await RemoveAsync(provider, Guid.NewGuid());

        await using var context = CreateDbContext();
        Assert.That(await context.Wishlists.AnyAsync(wishlist => wishlist.UserId == userId), Is.False);
    }

    [Test]
    public async Task Get_should_return_an_empty_result_without_creating_a_wishlist()
    {
        var userId = await SeedUserAsync("wishlist-get-empty");
        using var provider = CreateProvider(userId);

        var response = await GetAsync(provider);

        Assert.Multiple(() =>
        {
            Assert.That(response.WishlistId, Is.Null);
            Assert.That(response.Items, Is.Empty);
        });

        await using var context = CreateDbContext();
        Assert.That(await context.Wishlists.AnyAsync(wishlist => wishlist.UserId == userId), Is.False);
    }

    [Test]
    public async Task Get_should_return_product_details_image_and_lowest_active_offer_price()
    {
        var (userId, productId, _) = await SeedProductAsync(
            addImage: true,
            offerPrice: 125.50m);
        using var provider = CreateProvider(userId);
        await AddAsync(provider, productId);

        var response = await GetAsync(provider);
        var item = response.Items.Single();

        Assert.Multiple(() =>
        {
            Assert.That(response.WishlistId, Is.Not.Null);
            Assert.That(item.ProductId, Is.EqualTo(productId));
            Assert.That(item.ProductName, Does.StartWith("Wishlist Product "));
            Assert.That(item.ProductStatus, Is.EqualTo(ProductStatus.Active));
            Assert.That(item.BrandName, Does.StartWith("Wishlist Brand "));
            Assert.That(item.CategoryName, Does.StartWith("Wishlist Category "));
            Assert.That(item.PrimaryImageUrl, Does.StartWith("https://example.test/wishlist/"));
            Assert.That(item.LowestActiveOfferPrice, Is.EqualTo(125.50m));
        });
    }

    [Test]
    public async Task Get_should_only_return_the_current_users_wishlist()
    {
        var (currentUserId, currentProductId, _) = await SeedProductAsync();
        var (otherUserId, otherProductId, _) = await SeedProductAsync();

        await using (var context = CreateDbContext())
        {
            var currentWishlist = UserWishlist.Create(currentUserId);
            currentWishlist.AddProduct(currentProductId);
            var otherWishlist = UserWishlist.Create(otherUserId);
            otherWishlist.AddProduct(otherProductId);
            context.Wishlists.AddRange(currentWishlist, otherWishlist);
            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider(currentUserId);
        var response = await GetAsync(provider);

        Assert.That(response.Items.Select(item => item.ProductId),
            Is.EqualTo(new[] { currentProductId }));
    }

    [Test]
    public void Get_should_reject_a_request_without_an_authenticated_user()
    {
        using var provider = CreateProvider(currentUserId: null);

        Assert.ThrowsAsync<UnauthorizedException>(async () => await GetAsync(provider));
    }

    private async Task<(Guid UserId, Guid ProductId, Guid SellerOfferId)> SeedProductAsync(
        bool activateProduct = true,
        bool addImage = false,
        decimal offerPrice = 100m)
    {
        var userId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var userName = $"wishlist-test-{suffix}";

        await using var context = CreateDbContext();
        context.Users.Add(CreateUser(userId, userName));

        var brand = Brand.Create($"Wishlist Brand {suffix}");
        var category = Category.Create($"Wishlist Category {suffix}");
        var product = Product.Create(
            $"Wishlist Product {suffix}",
            "Wishlist product description.",
            brand.Id,
            category.Id);

        if (activateProduct)
            product.Activate();

        context.AddRange(brand, category, product);

        Guid offerId = Guid.Empty;
        if (activateProduct)
        {
            var variant = ProductVariant.Create(product, $"WISH-{suffix}", "Default");
            variant.Activate(product);
            var offer = SellerOffer.Create(userId, variant.Id, offerPrice, 10);
            offer.Activate();
            context.AddRange(variant, offer);
            offerId = offer.Id;
        }

        if (addImage)
        {
            context.ProductImages.Add(ProductImage.Create(
                product.Id,
                $"https://example.test/wishlist/{suffix}.jpg",
                "Wishlist product",
                0,
                true));
        }

        await context.SaveChangesAsync();
        return (userId, product.Id, offerId);
    }

    private async Task<Guid> SeedUserAsync(string prefix)
    {
        var userId = Guid.NewGuid();
        var userName = $"{prefix}-{Guid.NewGuid():N}";
        await using var context = CreateDbContext();
        context.Users.Add(CreateUser(userId, userName));
        await context.SaveChangesAsync();
        return userId;
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
            FirstName = "Wishlist",
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

    private static async Task<AddWishlistItemResponse> AddAsync(
        IServiceProvider provider,
        Guid productId)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new AddWishlistItemCommand(productId));
    }

    private static async Task RemoveAsync(IServiceProvider provider, Guid productId)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(new RemoveWishlistItemCommand(productId));
    }

    private static async Task<GetMyWishlistResponse> GetAsync(IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(new GetMyWishlistQuery());
    }

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;
        public bool IsAuthenticated => UserId.HasValue;
        public bool IsInRole(string role) => false;
    }
}
