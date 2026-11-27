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

public sealed class DemoDatabaseSeederTests : PostgresTestBase
{
    [Test]
    public async Task SeedDemoDataAsync_CreatesConfiguredAccountsAndIdempotentCatalog()
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
        var options = new DemoSeedOptions
        {
            AdminPassword = "Admin-Demo-7f!x",
            SellerPassword = "Seller-Demo-8g!y",
            SecondSellerPassword = "Second-Demo-9h!z",
            CustomerPassword = "Customer-Demo-4j!q"
        };

        await DatabaseSeeder.SeedRolesAsync(roleManager);

        // Invalid or absent credentials must be rejected before any Demo account is created.
        var invalidOptions = new DemoSeedOptions();
        Assert.ThrowsAsync<InvalidOperationException>(
            async () => await DatabaseSeeder.SeedDemoDataAsync(dbContext, userManager, invalidOptions));
        Assert.That(await userManager.Users.CountAsync(), Is.Zero);

        await DatabaseSeeder.SeedDemoDataAsync(dbContext, userManager, options);

        var admin = await userManager.FindByEmailAsync("admin@test.com");
        var firstSeller = await userManager.FindByEmailAsync("seller@test.com");
        var secondSeller = await userManager.FindByEmailAsync("seller2@test.com");
        var customer = await userManager.FindByEmailAsync("customer@test.com");

        Assert.Multiple(() =>
        {
            Assert.That(admin, Is.Not.Null);
            Assert.That(firstSeller, Is.Not.Null);
            Assert.That(secondSeller, Is.Not.Null);
            Assert.That(customer, Is.Not.Null);
            Assert.That(firstSeller!.SellerStatus, Is.EqualTo(SellerStatus.Active));
            Assert.That(secondSeller!.SellerStatus, Is.EqualTo(SellerStatus.Active));
        });

        var adminHasRole = await userManager.IsInRoleAsync(admin!, ApplicationRoles.Admin);
        var firstSellerHasRole = await userManager.IsInRoleAsync(firstSeller!, ApplicationRoles.Seller);
        var secondSellerHasRole = await userManager.IsInRoleAsync(secondSeller!, ApplicationRoles.Seller);
        var customerHasRole = await userManager.IsInRoleAsync(customer!, ApplicationRoles.Customer);
        var adminPasswordMatches = await userManager.CheckPasswordAsync(admin!, options.AdminPassword);
        var firstSellerPasswordMatches = await userManager.CheckPasswordAsync(firstSeller!, options.SellerPassword);
        var secondSellerPasswordMatches = await userManager.CheckPasswordAsync(secondSeller!, options.SecondSellerPassword);
        var customerPasswordMatches = await userManager.CheckPasswordAsync(customer!, options.CustomerPassword);
        var developmentPasswordMatches = await userManager.CheckPasswordAsync(customer!, "Password123!");

        Assert.Multiple(() =>
        {
            Assert.That(adminHasRole, Is.True);
            Assert.That(firstSellerHasRole, Is.True);
            Assert.That(secondSellerHasRole, Is.True);
            Assert.That(customerHasRole, Is.True);
            Assert.That(adminPasswordMatches, Is.True);
            Assert.That(firstSellerPasswordMatches, Is.True);
            Assert.That(secondSellerPasswordMatches, Is.True);
            Assert.That(customerPasswordMatches, Is.True);
            Assert.That(developmentPasswordMatches, Is.False);
        });

        var initialCounts = await GetSeedCountsAsync(dbContext, userManager);

        Assert.Multiple(() =>
        {
            Assert.That(initialCounts.Brands, Is.EqualTo(5));
            Assert.That(initialCounts.Categories, Is.EqualTo(8));
            Assert.That(initialCounts.Products, Is.EqualTo(6));
            Assert.That(initialCounts.Variants, Is.EqualTo(12));
            Assert.That(initialCounts.Images, Is.EqualTo(18));
            Assert.That(initialCounts.Offers, Is.EqualTo(18));
            Assert.That(initialCounts.Users, Is.EqualTo(4));
        });

        // Repeating the Demo seed must not duplicate users or catalog records.
        await DatabaseSeeder.SeedDemoDataAsync(dbContext, userManager, options);
        var repeatedCounts = await GetSeedCountsAsync(dbContext, userManager);

        Assert.That(repeatedCounts, Is.EqualTo(initialCounts));
    }

    private static async Task<SeedCounts> GetSeedCountsAsync(
        ECommerceDbContext dbContext,
        UserManager<ApplicationUser> userManager)
        => new(
            await dbContext.Brands.CountAsync(),
            await dbContext.Categories.CountAsync(),
            await dbContext.Products.CountAsync(),
            await dbContext.ProductVariants.CountAsync(),
            await dbContext.ProductImages.CountAsync(),
            await dbContext.SellerOffers.CountAsync(),
            await userManager.Users.CountAsync());

    private sealed record SeedCounts(
        int Brands,
        int Categories,
        int Products,
        int Variants,
        int Images,
        int Offers,
        int Users);
}
