using ECommerce.Application.Abstractions.Identity;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Identity;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Infrastructure.Persistence.Seed;
using ECommerce.Tests.Infrastructure.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Infrastructure.Persistence;

public sealed class DatabaseSeederTests : PostgresTestBase
{
    [Test]
    public async Task SeedDevelopmentDataAsync_CreatesCompleteIdempotentMarketplaceCatalog()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection();
        services.AddDbContext<ECommerceDbContext>(options =>
            options.UseNpgsql(ConnectionString));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ECommerceDbContext>()
            .AddDefaultTokenProviders();

        await using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();

        await DatabaseSeeder.SeedRolesAsync(roleManager);
        await DatabaseSeeder.SeedDevelopmentDataAsync(dbContext, userManager);

        var brandCount = await dbContext.Brands.CountAsync();
        var categoryCount = await dbContext.Categories.CountAsync();
        var childCategoryCount = await dbContext.Categories.CountAsync(
            category => category.ParentCategoryId != null);
        var productCount = await dbContext.Products.CountAsync();
        var variantCount = await dbContext.ProductVariants.CountAsync();
        var imageCount = await dbContext.ProductImages.CountAsync();
        var offerCount = await dbContext.SellerOffers.CountAsync();
        var activeOfferCount = await dbContext.SellerOffers.CountAsync(
            offer => offer.Status == SellerOfferStatus.Active);
        var sellersWithOffersCount = await dbContext.SellerOffers
            .Select(offer => offer.SellerId)
            .Distinct()
            .CountAsync();
        var httpsImageCount = await dbContext.ProductImages.CountAsync(
            image => image.Url.StartsWith("https://"));
        var activeProductCount = await dbContext.Products.CountAsync(
            product => product.Status == ProductStatus.Active);
        var activeVariantCount = await dbContext.ProductVariants.CountAsync(
            variant => variant.Status == ProductVariantStatus.Active);
        var userCount = await userManager.Users.CountAsync();

        Assert.Multiple(() =>
        {
            Assert.That(brandCount, Is.EqualTo(5));
            Assert.That(categoryCount, Is.EqualTo(8));
            Assert.That(childCategoryCount, Is.EqualTo(4));
            Assert.That(productCount, Is.EqualTo(6));
            Assert.That(variantCount, Is.EqualTo(12));
            Assert.That(imageCount, Is.EqualTo(18));
            Assert.That(offerCount, Is.EqualTo(18));
            Assert.That(activeOfferCount, Is.EqualTo(18));
            Assert.That(sellersWithOffersCount, Is.EqualTo(2));
            Assert.That(httpsImageCount, Is.EqualTo(18));
            Assert.That(activeProductCount, Is.EqualTo(6));
            Assert.That(activeVariantCount, Is.EqualTo(12));
            Assert.That(userCount, Is.EqualTo(4));
        });

        var firstSeller = await userManager.FindByEmailAsync("seller@test.com");
        var secondSeller = await userManager.FindByEmailAsync("seller2@test.com");
        var customer = await userManager.FindByEmailAsync("customer@test.com");

        Assert.Multiple(() =>
        {
            Assert.That(firstSeller, Is.Not.Null);
            Assert.That(firstSeller!.SellerStatus, Is.EqualTo(SellerStatus.Active));
            Assert.That(secondSeller, Is.Not.Null);
            Assert.That(secondSeller!.SellerStatus, Is.EqualTo(SellerStatus.Active));
            Assert.That(customer, Is.Not.Null);
        });

        Assert.That(await userManager.IsInRoleAsync(firstSeller!, ApplicationRoles.Seller), Is.True);
        Assert.That(await userManager.IsInRoleAsync(secondSeller!, ApplicationRoles.Seller), Is.True);
        Assert.That(await userManager.IsInRoleAsync(customer!, ApplicationRoles.Customer), Is.True);

        // Running the Development seeder again must not duplicate users or catalog records.
        await DatabaseSeeder.SeedDevelopmentDataAsync(dbContext, userManager);

        var repeatedSeedCounts = new
        {
            Brands = await dbContext.Brands.CountAsync(),
            Categories = await dbContext.Categories.CountAsync(),
            Products = await dbContext.Products.CountAsync(),
            Variants = await dbContext.ProductVariants.CountAsync(),
            Images = await dbContext.ProductImages.CountAsync(),
            Offers = await dbContext.SellerOffers.CountAsync(),
            Users = await userManager.Users.CountAsync()
        };

        Assert.Multiple(() =>
        {
            Assert.That(repeatedSeedCounts.Brands, Is.EqualTo(5));
            Assert.That(repeatedSeedCounts.Categories, Is.EqualTo(8));
            Assert.That(repeatedSeedCounts.Products, Is.EqualTo(6));
            Assert.That(repeatedSeedCounts.Variants, Is.EqualTo(12));
            Assert.That(repeatedSeedCounts.Images, Is.EqualTo(18));
            Assert.That(repeatedSeedCounts.Offers, Is.EqualTo(18));
            Assert.That(repeatedSeedCounts.Users, Is.EqualTo(4));
        });
    }
}
