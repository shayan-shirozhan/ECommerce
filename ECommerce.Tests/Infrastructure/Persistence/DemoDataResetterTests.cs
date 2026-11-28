using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Identity;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Infrastructure.Persistence.Seed;
using ECommerce.Tests.Infrastructure.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Infrastructure.Persistence;

public sealed class DemoDataResetterTests : PostgresTestBase
{
    [Test]
    public async Task Reset_should_delete_demo_activity_for_all_payment_states_and_preserve_seed_accounts_and_catalog()
    {
        await using var serviceProvider = await CreateSeededServiceProviderAsync();
        await using var scope = serviceProvider.CreateAsyncScope();

        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ECommerceDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        var seededAccountIds = await userManager.Users
            .ToDictionaryAsync(user => user.Email!, user => user.Id);
        var customer = (await userManager.FindByEmailAsync("customer@test.com"))!;
        var admin = (await userManager.FindByEmailAsync("admin@test.com"))!;
        var firstSeller = (await userManager.FindByEmailAsync("seller@test.com"))!;
        var secondSeller = (await userManager.FindByEmailAsync("seller2@test.com"))!;
        var visitor = await CreateVisitorAsync(userManager, "reset-all@example.test");

        var variant = await dbContext.ProductVariants.OrderBy(item => item.SKU).FirstAsync();
        var firstSellerOffer = await dbContext.SellerOffers.SingleAsync(
            offer => offer.SellerId == firstSeller.Id && offer.ProductVariantId == variant.Id);
        var secondSellerOffer = await dbContext.SellerOffers.FirstAsync(
            offer => offer.SellerId == secondSeller.Id);
        var seededOfferSnapshots = await dbContext.SellerOffers
            .Select(offer => new SeedOfferSnapshot(offer.Id, offer.SellerId, offer.Price, offer.Status))
            .ToArrayAsync();
        var seededProductIds = await dbContext.Products.Select(product => product.Id).ToArrayAsync();
        var seededVariantIds = await dbContext.ProductVariants.Select(item => item.Id).ToArrayAsync();

        firstSellerOffer.UpdateStock(0);
        secondSellerOffer.UpdateStock(2);

        var visitorOffer = SellerOffer.Create(visitor.Id, variant.Id, 123_000m, 4);
        visitorOffer.Activate();
        dbContext.SellerOffers.Add(visitorOffer);

        var pendingOrder = CreateOrder(visitor.Id, firstSeller.Id, variant, firstSellerOffer);
        var cancelledOrder = CreateOrder(visitor.Id, firstSeller.Id, variant, firstSellerOffer);
        cancelledOrder.Cancel();

        var pendingPaymentOrder = CreateOrderWithPayment(
            visitor.Id, firstSeller.Id, variant, firstSellerOffer, PaymentStatus.Pending);
        var successfulPaymentOrder = CreateOrderWithPayment(
            visitor.Id, firstSeller.Id, variant, firstSellerOffer, PaymentStatus.Successful);
        var failedPaymentOrder = CreateOrderWithPayment(
            visitor.Id, firstSeller.Id, variant, firstSellerOffer, PaymentStatus.Failed);
        var cancelledPaymentOrder = CreateOrderWithPayment(
            visitor.Id, firstSeller.Id, variant, firstSellerOffer, PaymentStatus.Cancelled);
        var manualReviewOrder = CreateOrderWithPayment(
            visitor.Id, firstSeller.Id, variant, firstSellerOffer, PaymentStatus.RequiresManualReview);

        dbContext.Orders.AddRange(
            pendingOrder,
            cancelledOrder,
            pendingPaymentOrder,
            successfulPaymentOrder,
            failedPaymentOrder,
            cancelledPaymentOrder,
            manualReviewOrder);

        var manualReviewPayment = manualReviewOrder.Payments.Single();
        dbContext.PaymentResolutionAudits.Add(PaymentResolutionAudit.Create(
            manualReviewPayment.Id,
            manualReviewOrder.Id,
            admin.Id,
            PaymentResolutionAction.VerifyAgain,
            "Created by the Demo reset integration test"));

        var customerCart = Cart.Create(customer.Id);
        customerCart.AddItem(firstSellerOffer.Id, 1);
        var visitorCart = Cart.Create(visitor.Id);
        visitorCart.AddItem(visitorOffer.Id, 2);
        dbContext.Carts.AddRange(customerCart, visitorCart);

        var customerWishlist = Wishlist.Create(customer.Id);
        customerWishlist.AddProduct(variant.ProductId);
        var visitorWishlist = Wishlist.Create(visitor.Id);
        visitorWishlist.AddProduct(variant.ProductId);
        dbContext.Wishlists.AddRange(customerWishlist, visitorWishlist);

        dbContext.Addresses.AddRange(
            CreateAddress(customer.Id, "Seed customer address"),
            CreateAddress(visitor.Id, "Visitor address"));
        dbContext.SellerRequests.AddRange(
            SellerRequest.Create(customer.Id, "Seed customer seller request"),
            SellerRequest.Create(visitor.Id, "Visitor seller request"));

        var nowUtc = DateTime.UtcNow;
        dbContext.RefreshTokens.AddRange(
            CreateRefreshToken(customer.Id, 'a', nowUtc),
            CreateRefreshToken(visitor.Id, 'b', nowUtc));

        await dbContext.SaveChangesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                new[]
                {
                    pendingPaymentOrder.Payments.Single().Status,
                    successfulPaymentOrder.Payments.Single().Status,
                    failedPaymentOrder.Payments.Single().Status,
                    cancelledPaymentOrder.Payments.Single().Status,
                    manualReviewPayment.Status
                },
                Is.EquivalentTo(new[]
                {
                    PaymentStatus.Pending,
                    PaymentStatus.Successful,
                    PaymentStatus.Failed,
                    PaymentStatus.Cancelled,
                    PaymentStatus.RequiresManualReview
                }));
            Assert.That(pendingOrder.Status, Is.EqualTo(OrderStatus.Pending));
            Assert.That(cancelledOrder.Status, Is.EqualTo(OrderStatus.Cancelled));
            Assert.That(successfulPaymentOrder.Status, Is.EqualTo(OrderStatus.Confirmed));
        });

        var result = await DemoDataResetter.ResetAsync(dbContext, userManager);

        Assert.Multiple(() =>
        {
            Assert.That(result.DeletedUsers, Is.EqualTo(1));
            Assert.That(result.DeletedOrders, Is.EqualTo(7));
            Assert.That(result.DeletedPayments, Is.EqualTo(5));
            Assert.That(result.DeletedPaymentAudits, Is.EqualTo(1));
            Assert.That(result.DeletedCarts, Is.EqualTo(2));
            Assert.That(result.DeletedWishlists, Is.EqualTo(2));
            Assert.That(result.DeletedAddresses, Is.EqualTo(2));
            Assert.That(result.DeletedSellerRequests, Is.EqualTo(2));
            Assert.That(result.DeletedRefreshTokens, Is.EqualTo(2));
            Assert.That(result.DeletedVisitorSellerOffers, Is.EqualTo(1));
            Assert.That(result.RestoredSeedOffers, Is.EqualTo(18));
        });

        Assert.That(await userManager.FindByEmailAsync(visitor.Email!), Is.Null);
        Assert.That(await userManager.Users.CountAsync(), Is.EqualTo(4));
        foreach (var (email, expectedId) in seededAccountIds)
        {
            var preservedUser = await userManager.FindByEmailAsync(email);
            Assert.That(preservedUser, Is.Not.Null, $"Seed account '{email}' should be preserved.");
            Assert.That(preservedUser!.Id, Is.EqualTo(expectedId));
        }

        var catalogCounts = new
        {
            Brands = await dbContext.Brands.CountAsync(),
            Categories = await dbContext.Categories.CountAsync(),
            Products = await dbContext.Products.CountAsync(),
            Variants = await dbContext.ProductVariants.CountAsync(),
            Images = await dbContext.ProductImages.CountAsync(),
            Offers = await dbContext.SellerOffers.CountAsync()
        };
        var activityCounts = new
        {
            Carts = await dbContext.Carts.CountAsync(),
            Wishlists = await dbContext.Wishlists.CountAsync(),
            Addresses = await dbContext.Addresses.CountAsync(),
            SellerRequests = await dbContext.SellerRequests.CountAsync(),
            Orders = await dbContext.Orders.CountAsync(),
            Payments = await dbContext.Payments.CountAsync(),
            PaymentAudits = await dbContext.PaymentResolutionAudits.CountAsync(),
            RefreshTokens = await dbContext.RefreshTokens.CountAsync()
        };
        var firstSellerStocks = await dbContext.SellerOffers
            .Where(offer => offer.SellerId == firstSeller.Id)
            .Select(offer => offer.Stock)
            .Distinct()
            .ToListAsync();
        var secondSellerStocks = await dbContext.SellerOffers
            .Where(offer => offer.SellerId == secondSeller.Id)
            .Select(offer => offer.Stock)
            .Distinct()
            .ToListAsync();
        var visitorOfferExists = await dbContext.SellerOffers.AnyAsync(offer => offer.Id == visitorOffer.Id);
        var offerSnapshotsAfterReset = await dbContext.SellerOffers
            .Select(offer => new SeedOfferSnapshot(offer.Id, offer.SellerId, offer.Price, offer.Status))
            .ToArrayAsync();
        var productIdsAfterReset = await dbContext.Products.Select(product => product.Id).ToArrayAsync();
        var variantIdsAfterReset = await dbContext.ProductVariants.Select(item => item.Id).ToArrayAsync();

        Assert.Multiple(() =>
        {
            Assert.That(catalogCounts.Brands, Is.EqualTo(5));
            Assert.That(catalogCounts.Categories, Is.EqualTo(8));
            Assert.That(catalogCounts.Products, Is.EqualTo(6));
            Assert.That(catalogCounts.Variants, Is.EqualTo(12));
            Assert.That(catalogCounts.Images, Is.EqualTo(18));
            Assert.That(catalogCounts.Offers, Is.EqualTo(18));
            Assert.That(offerSnapshotsAfterReset, Is.EquivalentTo(seededOfferSnapshots));
            Assert.That(productIdsAfterReset, Is.EquivalentTo(seededProductIds));
            Assert.That(variantIdsAfterReset, Is.EquivalentTo(seededVariantIds));
            Assert.That(activityCounts.Carts, Is.Zero);
            Assert.That(activityCounts.Wishlists, Is.Zero);
            Assert.That(activityCounts.Addresses, Is.Zero);
            Assert.That(activityCounts.SellerRequests, Is.Zero);
            Assert.That(activityCounts.Orders, Is.Zero);
            Assert.That(activityCounts.Payments, Is.Zero);
            Assert.That(activityCounts.PaymentAudits, Is.Zero);
            Assert.That(activityCounts.RefreshTokens, Is.Zero);
            Assert.That(visitorOfferExists, Is.False);
            Assert.That(firstSellerStocks, Is.EqualTo(new[] { DemoSeedDefaults.FirstSellerStock }));
            Assert.That(secondSellerStocks, Is.EqualTo(new[] { DemoSeedDefaults.SecondSellerStock }));
        });
    }

    [Test]
    public async Task Reset_should_fail_before_deleting_data_when_a_seed_account_is_missing()
    {
        await using var serviceProvider = await CreateSeededServiceProviderAsync();
        await using var scope = serviceProvider.CreateAsyncScope();

        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ECommerceDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var customer = (await userManager.FindByEmailAsync("customer@test.com"))!;
        var visitor = await CreateVisitorAsync(userManager, "reset-missing-seed@example.test");
        var variant = await dbContext.ProductVariants.OrderBy(item => item.SKU).FirstAsync();
        var firstSeller = (await userManager.FindByEmailAsync("seller@test.com"))!;
        var seedOffer = await dbContext.SellerOffers.FirstAsync(offer => offer.SellerId == firstSeller.Id);
        seedOffer.UpdateStock(3);

        var visitorOffer = SellerOffer.Create(visitor.Id, variant.Id, 123_000m, 4);
        visitorOffer.Activate();
        dbContext.SellerOffers.Add(visitorOffer);

        var visitorCart = Cart.Create(visitor.Id);
        visitorCart.AddItem(seedOffer.Id, 1);
        dbContext.Carts.Add(visitorCart);
        await dbContext.SaveChangesAsync();

        var deleteCustomerResult = await userManager.DeleteAsync(customer);
        Assert.That(deleteCustomerResult.Succeeded, Is.True,
            string.Join("; ", deleteCustomerResult.Errors.Select(error => error.Description)));

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await DemoDataResetter.ResetAsync(dbContext, userManager));

        Assert.That(exception!.Message, Does.Contain("four seeded Demo accounts must exist"));
        var visitorAfterFailure = await userManager.FindByEmailAsync(visitor.Email!);
        var visitorOfferExists = await dbContext.SellerOffers.AnyAsync(offer => offer.Id == visitorOffer.Id);
        var visitorCartExists = await dbContext.Carts.AnyAsync(cart => cart.UserId == visitor.Id);
        var seedOfferStock = await dbContext.SellerOffers
            .Where(offer => offer.Id == seedOffer.Id)
            .Select(offer => offer.Stock)
            .SingleAsync();
        var productCount = await dbContext.Products.CountAsync();

        Assert.Multiple(() =>
        {
            Assert.That(visitorAfterFailure, Is.Not.Null);
            Assert.That(visitorOfferExists, Is.True);
            Assert.That(visitorCartExists, Is.True);
            Assert.That(seedOfferStock, Is.EqualTo(3));
            Assert.That(productCount, Is.EqualTo(6));
        });
    }

    [Test]
    public async Task Reset_should_roll_back_deletions_and_account_removal_when_seed_stock_restore_fails()
    {
        await using var serviceProvider = await CreateSeededServiceProviderAsync();
        await using var scope = serviceProvider.CreateAsyncScope();

        var services = scope.ServiceProvider;
        var dbContext = services.GetRequiredService<ECommerceDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = (await userManager.FindByEmailAsync("admin@test.com"))!;
        var firstSeller = (await userManager.FindByEmailAsync("seller@test.com"))!;
        var visitor = await CreateVisitorAsync(userManager, "reset-rollback@example.test");
        var variant = await dbContext.ProductVariants.OrderBy(item => item.SKU).FirstAsync();
        var firstSellerOffers = await dbContext.SellerOffers
            .Where(offer => offer.SellerId == firstSeller.Id)
            .ToListAsync();
        foreach (var offer in firstSellerOffers)
            offer.UpdateStock(2);

        var seedOffer = firstSellerOffers[0];
        var seedOfferId = seedOffer.Id;
        var visitorOffer = SellerOffer.Create(visitor.Id, variant.Id, 123_000m, 4);
        visitorOffer.Activate();
        dbContext.SellerOffers.Add(visitorOffer);

        var order = CreateOrderWithPayment(
            visitor.Id, firstSeller.Id, variant, seedOffer, PaymentStatus.RequiresManualReview);
        dbContext.Orders.Add(order);
        var payment = order.Payments.Single();
        dbContext.PaymentResolutionAudits.Add(PaymentResolutionAudit.Create(
            payment.Id,
            order.Id,
            admin.Id,
            PaymentResolutionAction.VerifyAgain,
            "Created to verify Demo reset rollback"));

        var visitorCart = Cart.Create(visitor.Id);
        visitorCart.AddItem(visitorOffer.Id, 1);
        dbContext.Carts.Add(visitorCart);
        var nowUtc = DateTime.UtcNow;
        dbContext.RefreshTokens.Add(CreateRefreshToken(visitor.Id, 'c', nowUtc));
        await dbContext.SaveChangesAsync();

        // Force the reset to fail during the stock-restoration SaveChanges, after the
        // visitor's dependent rows and account have already been deleted in the transaction.
        await dbContext.Database.ExecuteSqlRawAsync(
            "ALTER TABLE \"SellerOffers\" ADD CONSTRAINT \"CK_DemoReset_RejectStockRestore\" CHECK (\"Stock\" <> 15);");

        DbUpdateException? resetException;
        try
        {
            resetException = Assert.ThrowsAsync<DbUpdateException>(async () =>
                await DemoDataResetter.ResetAsync(dbContext, userManager));
        }
        finally
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"SellerOffers\" DROP CONSTRAINT IF EXISTS \"CK_DemoReset_RejectStockRestore\";");
        }

        Assert.That(resetException, Is.Not.Null);

        await using var verificationScope = serviceProvider.CreateAsyncScope();
        var verificationServices = verificationScope.ServiceProvider;
        var verificationDbContext = verificationServices.GetRequiredService<ECommerceDbContext>();
        var verificationUserManager = verificationServices.GetRequiredService<UserManager<ApplicationUser>>();
        var restoredSeedOfferStocks = await verificationDbContext.SellerOffers
            .Where(offer => offer.SellerId == firstSeller.Id)
            .Select(offer => offer.Stock)
            .Distinct()
            .ToListAsync();
        var restoredSeedOffer = await verificationDbContext.SellerOffers
            .SingleAsync(offer => offer.Id == seedOfferId);
        var visitorAfterFailure = await verificationUserManager.FindByEmailAsync(visitor.Email!);
        var visitorOfferExists = await verificationDbContext.SellerOffers.AnyAsync(offer => offer.Id == visitorOffer.Id);
        var orderExists = await verificationDbContext.Orders.AnyAsync(item => item.Id == order.Id);
        var paymentExists = await verificationDbContext.Payments.AnyAsync(item => item.Id == payment.Id);
        var auditExists = await verificationDbContext.PaymentResolutionAudits.AnyAsync(item => item.PaymentId == payment.Id);
        var visitorCartExists = await verificationDbContext.Carts.AnyAsync(cart => cart.UserId == visitor.Id);
        var visitorRefreshTokenExists = await verificationDbContext.RefreshTokens.AnyAsync(token => token.UserId == visitor.Id);

        Assert.Multiple(() =>
        {
            Assert.That(visitorAfterFailure, Is.Not.Null);
            Assert.That(visitorOfferExists, Is.True);
            Assert.That(orderExists, Is.True);
            Assert.That(paymentExists, Is.True);
            Assert.That(auditExists, Is.True);
            Assert.That(visitorCartExists, Is.True);
            Assert.That(visitorRefreshTokenExists, Is.True);
            Assert.That(restoredSeedOffer.Stock, Is.EqualTo(2));
            Assert.That(restoredSeedOfferStocks, Is.EqualTo(new[] { 2 }));
        });
    }

    private async Task<ServiceProvider> CreateSeededServiceProviderAsync()
    {
        var serviceProvider = CreateServiceProvider();
        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var services = scope.ServiceProvider;
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var dbContext = services.GetRequiredService<ECommerceDbContext>();

            await DatabaseSeeder.SeedRolesAsync(roleManager);
            await DatabaseSeeder.SeedDemoDataAsync(dbContext, userManager, CreateDemoSeedOptions());

            // Each test starts from the persistent seed state even though the fixture shares one container.
            await DemoDataResetter.ResetAsync(dbContext, userManager);
            return serviceProvider;
        }
        catch
        {
            await serviceProvider.DisposeAsync();
            throw;
        }
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

    private static async Task<ApplicationUser> CreateVisitorAsync(
        UserManager<ApplicationUser> userManager,
        string email)
    {
        var visitor = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "Demo",
            LastName = "Visitor"
        };
        var result = await userManager.CreateAsync(visitor, "Visitor-Demo-5!");
        Assert.That(result.Succeeded, Is.True,
            string.Join("; ", result.Errors.Select(error => error.Description)));
        return visitor;
    }

    private static Order CreateOrder(
        Guid userId,
        Guid sellerId,
        ProductVariant variant,
        SellerOffer offer)
    {
        var order = Order.Create(
            userId,
            "Demo Visitor",
            "09120000000",
            "Tehran",
            "Tehran",
            "1234567890",
            "Demo Street");
        order.AddItem(
            sellerId,
            variant.ProductId,
            variant.Id,
            offer.Id,
            "Reset Test Product",
            "Default",
            "Seed Seller",
            123_000m,
            1,
            0m);
        return order;
    }

    private static Order CreateOrderWithPayment(
        Guid userId,
        Guid sellerId,
        ProductVariant variant,
        SellerOffer offer,
        PaymentStatus status)
    {
        var order = CreateOrder(userId, sellerId, variant, offer);
        var paymentMethod = status == PaymentStatus.RequiresManualReview
            ? PaymentMethod.ZarinPal
            : PaymentMethod.Mock;
        var payment = order.InitiatePayment(paymentMethod);

        switch (status)
        {
            case PaymentStatus.Pending:
                break;
            case PaymentStatus.Successful:
                payment.MarkSuccessful(transactionId: $"reset-{Guid.NewGuid():N}");
                order.Confirm();
                break;
            case PaymentStatus.Failed:
                payment.MarkFailed();
                break;
            case PaymentStatus.Cancelled:
                payment.Cancel();
                break;
            case PaymentStatus.RequiresManualReview:
                payment.SetAuthority($"reset-authority-{Guid.NewGuid():N}");
                payment.MarkVerificationAttempted(DateTime.UtcNow);
                payment.MarkRequiresManualReview();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported test payment status.");
        }

        return order;
    }

    private static RefreshToken CreateRefreshToken(Guid userId, char tokenCharacter, DateTime nowUtc)
        => RefreshToken.Create(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            new string(tokenCharacter, 64),
            nowUtc,
            nowUtc.AddDays(1));

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

    private sealed record SeedOfferSnapshot(
        Guid Id,
        Guid SellerId,
        decimal Price,
        SellerOfferStatus Status);

    private static DemoSeedOptions CreateDemoSeedOptions() => new()
    {
        AdminPassword = "Admin-Demo-7f!x",
        SellerPassword = "Seller-Demo-8g!y",
        SecondSellerPassword = "Second-Demo-9h!z",
        CustomerPassword = "Customer-Demo-4j!q"
    };
}
