using ECommerce.Application.Abstractions.Identity;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedRolesAsync(
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        await EnsureRoleAsync(roleManager, ApplicationRoles.Admin);
        await EnsureRoleAsync(roleManager, ApplicationRoles.Seller);
        await EnsureRoleAsync(roleManager, ApplicationRoles.Customer);
    }

    public static async Task SeedDevelopmentDataAsync(
        ECommerceDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken = default)
    {
        var users = await SeedUsersAsync(userManager);
        await SeedCatalogDataAsync(dbContext, users, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureRoleAsync(
        RoleManager<IdentityRole<Guid>> roleManager,
        string role)
    {
        if (await roleManager.RoleExistsAsync(role))
            return;

        var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));

        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Failed to seed role '{role}': {errors}");
        }
    }

    private static async Task<DevelopmentUsers> SeedUsersAsync(
        UserManager<ApplicationUser> userManager)
    {
        var admin = await EnsureUserAsync(
            userManager,
            email: "admin@test.com",
            password: "Password123!",
            firstName: "Demo",
            lastName: "Admin");
        await EnsureRoleAsync(userManager, admin, ApplicationRoles.Admin);

        var firstSeller = await EnsureDevelopmentSellerAsync(
            userManager,
            email: "seller@test.com",
            firstName: "Demo",
            lastName: "Electronics");

        var secondSeller = await EnsureDevelopmentSellerAsync(
            userManager,
            email: "seller2@test.com",
            firstName: "Demo",
            lastName: "Digital");

        var customer = await EnsureUserAsync(
            userManager,
            email: "customer@test.com",
            password: "Password123!",
            firstName: "Demo",
            lastName: "Customer");
        await EnsureRoleAsync(userManager, customer, ApplicationRoles.Customer);

        return new DevelopmentUsers(
            firstSeller.Id,
            firstSeller.SellerStatus == SellerStatus.Active,
            secondSeller.Id,
            secondSeller.SellerStatus == SellerStatus.Active);
    }

    private static async Task<ApplicationUser> EnsureDevelopmentSellerAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string firstName,
        string lastName)
    {
        var seller = await EnsureUserAsync(
            userManager,
            email: email,
            password: "Password123!",
            firstName: firstName,
            lastName: lastName);

        // Do not override a deliberately suspended or rejected demo account.
        if (seller.SellerStatus == SellerStatus.None)
        {
            seller.SellerStatus = SellerStatus.Active;
            var updateResult = await userManager.UpdateAsync(seller);
            EnsureIdentitySuccess(updateResult, $"updating seller '{email}'");
        }

        await EnsureRoleAsync(userManager, seller, ApplicationRoles.Seller);
        return seller;
    }

    private static async Task SeedCatalogDataAsync(
        ECommerceDbContext dbContext,
        DevelopmentUsers users,
        CancellationToken cancellationToken)
    {
        var apple = await EnsureBrandAsync(dbContext, "Apple", cancellationToken);
        var samsung = await EnsureBrandAsync(dbContext, "Samsung", cancellationToken);
        var sony = await EnsureBrandAsync(dbContext, "Sony", cancellationToken);
        var logitech = await EnsureBrandAsync(dbContext, "Logitech", cancellationToken);
        var philips = await EnsureBrandAsync(dbContext, "Philips", cancellationToken);

        // Keep the original Mobile Phones root category for existing Development databases.
        var mobilePhones = await EnsureCategoryAsync(
            dbContext, "Mobile Phones", null, cancellationToken);
        var computers = await EnsureCategoryAsync(
            dbContext, "Computers & Accessories", null, cancellationToken);
        var audio = await EnsureCategoryAsync(
            dbContext, "Audio", null, cancellationToken);
        var homeAndKitchen = await EnsureCategoryAsync(
            dbContext, "Home & Kitchen", null, cancellationToken);

        var keyboardsAndMice = await EnsureCategoryAsync(
            dbContext, "Keyboards & Mice", computers.Id, cancellationToken);
        var laptops = await EnsureCategoryAsync(
            dbContext, "Laptops", computers.Id, cancellationToken);
        var headphones = await EnsureCategoryAsync(
            dbContext, "Headphones", audio.Id, cancellationToken);
        var smallAppliances = await EnsureCategoryAsync(
            dbContext, "Small Appliances", homeAndKitchen.Id, cancellationToken);

        var products = new[]
        {
            new ProductSeed(
                "Apple iPhone 17 Pro",
                "A premium smartphone with an advanced camera system and high-performance chipset.",
                apple,
                mobilePhones,
                "https://images.unsplash.com/photo-1592750475338-74b7b21085ab?auto=format&fit=crop&w=1200&q=85",
                [
                    new VariantSeed("APL-IP17P-256-NAT", "256 GB - Natural Titanium", 98_000_000m, 96_500_000m),
                    new VariantSeed("APL-IP17P-512-BLU", "512 GB - Blue Titanium", 112_000_000m, null)
                ]),
            new ProductSeed(
                "Samsung Galaxy S25",
                "A compact Android smartphone with a bright display and versatile camera features.",
                samsung,
                mobilePhones,
                "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&w=1200&q=85",
                [
                    new VariantSeed("SAM-GS25-256-NVY", "256 GB - Navy", 70_000_000m, 68_900_000m),
                    new VariantSeed("SAM-GS25-512-SLV", "512 GB - Silver", 78_000_000m, null)
                ]),
            new ProductSeed(
                "Sony WH-1000XM6",
                "Wireless over-ear headphones designed for immersive listening and noise cancellation.",
                sony,
                headphones,
                "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?auto=format&fit=crop&w=1200&q=85",
                [
                    new VariantSeed("SNY-WH1000XM6-BLK", "Black", 24_500_000m, 25_000_000m),
                    new VariantSeed("SNY-WH1000XM6-SLV", "Silver", 26_000_000m, null)
                ]),
            new ProductSeed(
                "Apple MacBook Air 13-inch",
                "A lightweight laptop for everyday productivity with long battery life.",
                apple,
                laptops,
                "https://images.unsplash.com/photo-1517336714731-489689fd1ca8?auto=format&fit=crop&w=1200&q=85",
                [
                    new VariantSeed("APL-MBA13-16-512-MDN", "16 GB RAM - 512 GB - Midnight", 72_000_000m, 71_000_000m),
                    new VariantSeed("APL-MBA13-24-512-SLV", "24 GB RAM - 512 GB - Silver", 79_000_000m, null)
                ]),
            new ProductSeed(
                "Logitech MX Master 3S",
                "An ergonomic wireless productivity mouse with customizable controls.",
                logitech,
                keyboardsAndMice,
                "https://images.unsplash.com/photo-1527814050087-3793815479db?auto=format&fit=crop&w=1200&q=85",
                [
                    new VariantSeed("LOG-MXM3S-GPH", "Graphite", 7_800_000m, 7_650_000m),
                    new VariantSeed("LOG-MXM3S-PGY", "Pale Grey", 8_100_000m, null)
                ]),
            new ProductSeed(
                "Philips Airfryer 3000 Series",
                "A compact air fryer for preparing everyday meals with little or no added oil.",
                philips,
                smallAppliances,
                "https://images.unsplash.com/photo-1585515320310-259814833e62?auto=format&fit=crop&w=1200&q=85",
                [
                    new VariantSeed("PHI-AF3000-BLK", "Black", 13_500_000m, 13_250_000m),
                    new VariantSeed("PHI-AF3000-WHT", "White", 14_000_000m, null)
                ])
        };

        foreach (var productSeed in products)
        {
            await SeedProductAsync(
                dbContext,
                productSeed,
                users,
                cancellationToken);
        }
    }

    private static async Task<Brand> EnsureBrandAsync(
        ECommerceDbContext dbContext,
        string name,
        CancellationToken cancellationToken)
    {
        var brand = await dbContext.Brands.FirstOrDefaultAsync(
            item => item.Name == name && !item.IsDeleted,
            cancellationToken);

        if (brand is not null)
            return brand;

        brand = Brand.Create(name);
        dbContext.Brands.Add(brand);
        return brand;
    }

    private static async Task<Category> EnsureCategoryAsync(
        ECommerceDbContext dbContext,
        string name,
        Guid? parentCategoryId,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories.FirstOrDefaultAsync(
            item => item.Name == name &&
                    item.ParentCategoryId == parentCategoryId &&
                    !item.IsDeleted,
            cancellationToken);

        if (category is not null)
            return category;

        category = Category.Create(name, parentCategoryId);
        dbContext.Categories.Add(category);
        return category;
    }

    private static async Task SeedProductAsync(
        ECommerceDbContext dbContext,
        ProductSeed seed,
        DevelopmentUsers users,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products.FirstOrDefaultAsync(
            item => item.Name == seed.Name &&
                    item.BrandId == seed.Brand.Id &&
                    item.CategoryId == seed.Category.Id,
            cancellationToken);

        if (product is null)
        {
            product = Product.Create(
                seed.Name,
                seed.Description,
                seed.Brand.Id,
                seed.Category.Id);
            product.Activate();
            dbContext.Products.Add(product);
        }

        await EnsureProductImageAsync(
            dbContext,
            product.Id,
            null,
            seed.ImageUrl,
            seed.Name,
            0,
            true,
            cancellationToken);

        foreach (var variantSeed in seed.Variants)
        {
            var variant = await EnsureVariantAsync(
                dbContext,
                product,
                variantSeed,
                cancellationToken);

            if (variant is null)
                continue;

            await EnsureProductImageAsync(
                dbContext,
                product.Id,
                variant.Id,
                seed.ImageUrl,
                $"{seed.Name} - {variantSeed.Name}",
                1,
                false,
                cancellationToken);

            // Existing inactive products/variants are left untouched on subsequent startup.
            if (product.Status != ProductStatus.Active ||
                variant.Status != ProductVariantStatus.Active)
            {
                continue;
            }

            if (users.FirstSellerIsActive)
            {
                await EnsureSellerOfferAsync(
                    dbContext,
                    users.FirstSellerId,
                    variant.Id,
                    variantSeed.FirstSellerPrice,
                    15,
                    cancellationToken);
            }

            if (users.SecondSellerIsActive &&
                variantSeed.SecondSellerPrice is decimal secondSellerPrice)
            {
                await EnsureSellerOfferAsync(
                    dbContext,
                    users.SecondSellerId,
                    variant.Id,
                    secondSellerPrice,
                    8,
                    cancellationToken);
            }
        }
    }

    private static async Task<ProductVariant?> EnsureVariantAsync(
        ECommerceDbContext dbContext,
        Product product,
        VariantSeed seed,
        CancellationToken cancellationToken)
    {
        var variant = await dbContext.ProductVariants.FirstOrDefaultAsync(
            item => item.SKU == seed.Sku,
            cancellationToken);

        if (variant is not null)
        {
            if (variant.ProductId != product.Id)
            {
                throw new InvalidOperationException(
                    $"Demo SKU '{seed.Sku}' is already assigned to a different product.");
            }

            return variant;
        }

        if (product.Status != ProductStatus.Active)
            return null;

        variant = ProductVariant.Create(product, seed.Sku, seed.Name);
        variant.Activate(product);
        dbContext.ProductVariants.Add(variant);
        return variant;
    }

    private static async Task EnsureProductImageAsync(
        ECommerceDbContext dbContext,
        Guid productId,
        Guid? productVariantId,
        string url,
        string altText,
        int sortOrder,
        bool isPrimary,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.ProductImages.AnyAsync(
            image => image.ProductId == productId &&
                     image.ProductVariantId == productVariantId &&
                     image.Url == url,
            cancellationToken);

        if (exists)
            return;

        dbContext.ProductImages.Add(ProductImage.Create(
            productId,
            url,
            altText,
            sortOrder,
            isPrimary,
            productVariantId));
    }

    private static async Task EnsureSellerOfferAsync(
        ECommerceDbContext dbContext,
        Guid sellerId,
        Guid productVariantId,
        decimal price,
        int stock,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.SellerOffers.AnyAsync(
            offer => offer.SellerId == sellerId &&
                     offer.ProductVariantId == productVariantId,
            cancellationToken);

        if (exists)
            return;

        var offer = SellerOffer.Create(sellerId, productVariantId, price, stock);
        offer.Activate();
        dbContext.SellerOffers.Add(offer);
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string firstName,
        string lastName)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is not null)
            return user;

        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName
        };

        var result = await userManager.CreateAsync(user, password);
        EnsureIdentitySuccess(result, $"creating user '{email}'");
        return user;
    }

    private static async Task EnsureRoleAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        string role)
    {
        if (await userManager.IsInRoleAsync(user, role))
            return;

        var result = await userManager.AddToRoleAsync(user, role);
        EnsureIdentitySuccess(result, $"assigning role '{role}' to user '{user.Id}'");
    }

    private static void EnsureIdentitySuccess(
        IdentityResult result,
        string operation)
    {
        if (result.Succeeded)
            return;

        var errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"Failed while {operation}: {errors}");
    }

    private sealed record DevelopmentUsers(
        Guid FirstSellerId,
        bool FirstSellerIsActive,
        Guid SecondSellerId,
        bool SecondSellerIsActive);

    private sealed record ProductSeed(
        string Name,
        string Description,
        Brand Brand,
        Category Category,
        string ImageUrl,
        VariantSeed[] Variants);

    private sealed record VariantSeed(
        string Sku,
        string Name,
        decimal FirstSellerPrice,
        decimal? SecondSellerPrice);
}
