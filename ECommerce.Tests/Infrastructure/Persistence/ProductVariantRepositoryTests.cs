using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Persistence.Repositories;
using ECommerce.Tests.Infrastructure.Fixtures;

namespace ECommerce.Tests.Infrastructure.Persistence;

public sealed class ProductVariantRepositoryTests : PostgresTestBase
{
    [Test]
    public async Task ExistsBySku_should_find_matching_sku_and_exclude_current_variant()
    {
        await using var dbContext = CreateDbContext();

        var brand = Brand.Create($"Brand-{Guid.NewGuid():N}");
        var category = Category.Create($"Category-{Guid.NewGuid():N}");
        dbContext.Brands.Add(brand);
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();

        var product = Product.Create(
            "Test product",
            "Test product description",
            brand.Id,
            category.Id);
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();

        var variant = ProductVariant.Create(product, "SKU-UNIQUE-001", "Standard");
        dbContext.ProductVariants.Add(variant);
        await dbContext.SaveChangesAsync();

        var repository = new ProductVariantRepository(dbContext);

        var skuExists = await repository.ExistsBySkuAsync("SKU-UNIQUE-001");
        var currentVariantSkuExists = await repository.ExistsBySkuAsync(
            "SKU-UNIQUE-001",
            variant.Id);
        var missingSkuExists = await repository.ExistsBySkuAsync("SKU-MISSING");

        Assert.Multiple(() =>
        {
            Assert.That(skuExists, Is.True);
            Assert.That(currentVariantSkuExists, Is.False);
            Assert.That(missingSkuExists, Is.False);
        });
    }
}
