using ECommerce.API.BackgroundServices;
using ECommerce.API.Options;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Identity;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Infrastructure.Persistence.Seed;
using ECommerce.Tests.Infrastructure.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Tests.API.BackgroundServices;

public sealed class DemoDataResetBackgroundServiceTests : PostgresTestBase
{
    [Test]
    public async Task Worker_should_remove_visitor_data_and_restore_seed_stock_on_schedule()
    {
        await using var serviceProvider = CreateServiceProvider();
        await using var setupScope = serviceProvider.CreateAsyncScope();

        var services = setupScope.ServiceProvider;
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = services.GetRequiredService<ECommerceDbContext>();

        await DatabaseSeeder.SeedRolesAsync(roleManager);
        await DatabaseSeeder.SeedDemoDataAsync(dbContext, userManager, CreateDemoSeedOptions());

        var customer = (await userManager.FindByEmailAsync("customer@test.com"))!;
        var admin = (await userManager.FindByEmailAsync("admin@test.com"))!;
        var firstSeller = (await userManager.FindByEmailAsync("seller@test.com"))!;
        var secondSeller = (await userManager.FindByEmailAsync("seller2@test.com"))!;

        const string visitorEmail = "visitor-reset-test@example.test";
        var visitor = new ApplicationUser
        {
            UserName = visitorEmail,
            Email = visitorEmail,
            EmailConfirmed = true,
            FirstName = "Demo",
            LastName = "Visitor"
        };
        var visitorCreation = await userManager.CreateAsync(visitor, "Visitor-Demo-5!");
        Assert.That(visitorCreation.Succeeded, Is.True,
            string.Join("; ", visitorCreation.Errors.Select(error => error.Description)));

        var firstVariant = await dbContext.ProductVariants
            .OrderBy(variant => variant.SKU)
            .FirstAsync();
        var firstSellerOffer = await dbContext.SellerOffers
            .SingleAsync(offer => offer.SellerId == firstSeller.Id &&
                                  offer.ProductVariantId == firstVariant.Id);
        var secondSellerOffer = await dbContext.SellerOffers
            .FirstAsync(offer => offer.SellerId == secondSeller.Id);

        firstSellerOffer.UpdateStock(0);
        secondSellerOffer.UpdateStock(0);

        var visitorOffer = SellerOffer.Create(visitor.Id, firstVariant.Id, 123_000m, 4);
        visitorOffer.Activate();
        dbContext.SellerOffers.Add(visitorOffer);
        await dbContext.SaveChangesAsync();

        var customerCart = Cart.Create(customer.Id);
        customerCart.AddItem(firstSellerOffer.Id, 1);
        var visitorCart = Cart.Create(visitor.Id);
        visitorCart.AddItem(visitorOffer.Id, 2);
        dbContext.Carts.AddRange(customerCart, visitorCart);

        var customerWishlist = Wishlist.Create(customer.Id);
        customerWishlist.AddProduct(firstVariant.ProductId);
        var visitorWishlist = Wishlist.Create(visitor.Id);
        visitorWishlist.AddProduct(firstVariant.ProductId);
        dbContext.Wishlists.AddRange(customerWishlist, visitorWishlist);

        dbContext.Addresses.AddRange(
            CreateAddress(customer.Id, "Seed customer address"),
            CreateAddress(visitor.Id, "Visitor address"));
        dbContext.SellerRequests.AddRange(
            SellerRequest.Create(customer.Id, "Created during the public demo"),
            SellerRequest.Create(visitor.Id, "Visitor seller request"));

        var order = Order.Create(
            visitor.Id,
            "Demo Visitor",
            "09120000000",
            "Tehran",
            "Tehran",
            "1234567890",
            "Demo Street");
        order.AddItem(
            firstSeller.Id,
            firstVariant.ProductId,
            firstVariant.Id,
            firstSellerOffer.Id,
            "Reset Test Product",
            "Default",
            "Demo Seller",
            123_000m,
            1,
            0m);
        var payment = order.InitiatePayment(PaymentMethod.Mock);
        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync();

        dbContext.PaymentResolutionAudits.Add(PaymentResolutionAudit.Create(
            payment.Id,
            order.Id,
            admin.Id,
            PaymentResolutionAction.VerifyAgain,
            "Created during the reset integration test"));

        var nowUtc = DateTime.UtcNow;
        dbContext.RefreshTokens.AddRange(
            RefreshToken.Create(
                Guid.NewGuid(), customer.Id, Guid.NewGuid(), new string('a', 64),
                nowUtc, nowUtc.AddDays(1)),
            RefreshToken.Create(
                Guid.NewGuid(), visitor.Id, Guid.NewGuid(), new string('b', 64),
                nowUtc, nowUtc.AddDays(1)));

        await dbContext.SaveChangesAsync();

        var service = new DemoDataResetBackgroundService(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new DemoResetOptions { IntervalSeconds = 1 }),
            serviceProvider.GetRequiredService<ILogger<DemoDataResetBackgroundService>>());

        await service.StartAsync(CancellationToken.None);
        try
        {
            await WaitForVisitorDeletionAsync(serviceProvider, visitorEmail);
        }
        finally
        {
            using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await service.StopAsync(stopTimeout.Token);
        }

        await using var verificationScope = serviceProvider.CreateAsyncScope();
        var verificationServices = verificationScope.ServiceProvider;
        var verificationDbContext = verificationServices.GetRequiredService<ECommerceDbContext>();
        var verificationUserManager = verificationServices.GetRequiredService<UserManager<ApplicationUser>>();

        var seededUserCount = await verificationUserManager.Users.CountAsync();
        var visitorAfterReset = await verificationUserManager.FindByEmailAsync(visitorEmail);
        var firstSellerStocks = await verificationDbContext.SellerOffers
            .Where(offer => offer.SellerId == firstSeller.Id)
            .Select(offer => offer.Stock)
            .Distinct()
            .ToListAsync();
        var secondSellerStocks = await verificationDbContext.SellerOffers
            .Where(offer => offer.SellerId == secondSeller.Id)
            .Select(offer => offer.Stock)
            .Distinct()
            .ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(seededUserCount, Is.EqualTo(4));
            Assert.That(visitorAfterReset, Is.Null);
            Assert.That(firstSellerStocks, Is.EqualTo(new[] { 15 }));
            Assert.That(secondSellerStocks, Is.EqualTo(new[] { 8 }));
        });

        var catalogCounts = new
        {
            Brands = await verificationDbContext.Brands.CountAsync(),
            Categories = await verificationDbContext.Categories.CountAsync(),
            Products = await verificationDbContext.Products.CountAsync(),
            Variants = await verificationDbContext.ProductVariants.CountAsync(),
            Offers = await verificationDbContext.SellerOffers.CountAsync()
        };
        var activityCounts = new
        {
            Carts = await verificationDbContext.Carts.CountAsync(),
            Wishlists = await verificationDbContext.Wishlists.CountAsync(),
            Addresses = await verificationDbContext.Addresses.CountAsync(),
            SellerRequests = await verificationDbContext.SellerRequests.CountAsync(),
            Orders = await verificationDbContext.Orders.CountAsync(),
            Payments = await verificationDbContext.Payments.CountAsync(),
            PaymentAudits = await verificationDbContext.PaymentResolutionAudits.CountAsync(),
            RefreshTokens = await verificationDbContext.RefreshTokens.CountAsync()
        };

        Assert.Multiple(() =>
        {
            Assert.That(catalogCounts.Brands, Is.EqualTo(5));
            Assert.That(catalogCounts.Categories, Is.EqualTo(8));
            Assert.That(catalogCounts.Products, Is.EqualTo(6));
            Assert.That(catalogCounts.Variants, Is.EqualTo(12));
            Assert.That(catalogCounts.Offers, Is.EqualTo(18));
            Assert.That(activityCounts.Carts, Is.Zero);
            Assert.That(activityCounts.Wishlists, Is.Zero);
            Assert.That(activityCounts.Addresses, Is.Zero);
            Assert.That(activityCounts.SellerRequests, Is.Zero);
            Assert.That(activityCounts.Orders, Is.Zero);
            Assert.That(activityCounts.Payments, Is.Zero);
            Assert.That(activityCounts.PaymentAudits, Is.Zero);
            Assert.That(activityCounts.RefreshTokens, Is.Zero);
        });
    }

    private async Task WaitForVisitorDeletionAsync(IServiceProvider serviceProvider, string visitorEmail)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (await userManager.FindByEmailAsync(visitorEmail) is null)
                return;

            await Task.Delay(100);
        }

        Assert.Fail("The Demo data reset worker did not delete the visitor account within the expected interval.");
    }

    private ServiceProvider CreateServiceProvider()
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

        return services.BuildServiceProvider();
    }

    private static DemoSeedOptions CreateDemoSeedOptions() => new()
    {
        AdminPassword = "Admin-Demo-7f!x",
        SellerPassword = "Seller-Demo-8g!y",
        SecondSellerPassword = "Second-Demo-9h!z",
        CustomerPassword = "Customer-Demo-4j!q"
    };

    private static Address CreateAddress(Guid userId, string title)
        => Address.Create(
            userId,
            title,
            "Demo Recipient",
            "09120000000",
            "Tehran",
            "Tehran",
            "1234567890",
            "Demo Street");
}
