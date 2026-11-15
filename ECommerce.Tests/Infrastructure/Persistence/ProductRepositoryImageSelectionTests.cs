using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Persistence.Repositories;
using ECommerce.Tests.Infrastructure.Fixtures;

namespace ECommerce.Tests.Infrastructure.Persistence;

public sealed class ProductRepositoryImageSelectionTests : PostgresTestBase
{
    [Test]
    public async Task GetActivePage_should_select_primary_available_image_and_fall_back_to_sort_order()
    {
        await using var dbContext = CreateDbContext();

        var brand = Brand.Create($"Brand-{Guid.NewGuid():N}");
        var category = Category.Create($"Category-{Guid.NewGuid():N}");

        var productWithImages = Product.Create(
            "Product with images",
            "Product image selection test",
            brand.Id,
            category.Id);
        productWithImages.Activate();

        var activeVariant = ProductVariant.Create(
            productWithImages,
            $"SKU-ACTIVE-{Guid.NewGuid():N}",
            "Active variant");
        activeVariant.Activate(productWithImages);

        var inactiveVariant = ProductVariant.Create(
            productWithImages,
            $"SKU-INACTIVE-{Guid.NewGuid():N}",
            "Inactive variant");
        inactiveVariant.Deactivate();

        var productLevelImage = ProductImage.Create(
            productWithImages.Id,
            "https://example.test/product-level.jpg",
            "Product-level image",
            sortOrder: 0,
            isPrimary: false);

        var activeVariantPrimaryImage = ProductImage.Create(
            productWithImages.Id,
            "https://example.test/active-variant-primary.jpg",
            "Active variant primary image",
            sortOrder: 10,
            isPrimary: true,
            productVariantId: activeVariant.Id);

        var inactiveVariantPrimaryImage = ProductImage.Create(
            productWithImages.Id,
            "https://example.test/inactive-variant-primary.jpg",
            "Inactive variant primary image",
            sortOrder: 0,
            isPrimary: true,
            productVariantId: inactiveVariant.Id);

        var productWithFallbackImage = Product.Create(
            "Product with fallback image",
            "Product without a primary image",
            brand.Id,
            category.Id);
        productWithFallbackImage.Activate();

        var fallbackImage = ProductImage.Create(
            productWithFallbackImage.Id,
            "https://example.test/fallback-image.jpg",
            "Fallback image",
            sortOrder: 2,
            isPrimary: false);

        var laterFallbackImage = ProductImage.Create(
            productWithFallbackImage.Id,
            "https://example.test/later-fallback-image.jpg",
            "Later fallback image",
            sortOrder: 5,
            isPrimary: false);

        var productWithoutImage = Product.Create(
            "Product without image",
            "No product image",
            brand.Id,
            category.Id);
        productWithoutImage.Activate();

        dbContext.Brands.Add(brand);
        dbContext.Categories.Add(category);
        dbContext.Products.AddRange(
            productWithImages,
            productWithFallbackImage,
            productWithoutImage);
        dbContext.ProductVariants.AddRange(activeVariant, inactiveVariant);
        dbContext.ProductImages.AddRange(
            productLevelImage,
            activeVariantPrimaryImage,
            inactiveVariantPrimaryImage,
            fallbackImage,
            laterFallbackImage);

        await dbContext.SaveChangesAsync();

        var repository = new ProductRepository(dbContext);
        var filter = new ProductListFilter(
            CategoryId: null,
            BrandId: null,
            MinPrice: null,
            MaxPrice: null,
            Search: null,
            SortOrder: ProductSortOrder.Newest);

        var products = await repository.GetActivePageAsync(1, 10, filter);

        var primaryImageProduct = products.Single(product =>
            product.Id == productWithImages.Id);
        var fallbackImageProduct = products.Single(product =>
            product.Id == productWithFallbackImage.Id);
        var noImageProduct = products.Single(product =>
            product.Id == productWithoutImage.Id);

        Assert.Multiple(() =>
        {
            Assert.That(primaryImageProduct.Image, Is.Not.Null);
            Assert.That(
                primaryImageProduct.Image!.Url,
                Is.EqualTo(activeVariantPrimaryImage.Url));
            Assert.That(
                primaryImageProduct.Image.AltText,
                Is.EqualTo(activeVariantPrimaryImage.AltText));

            Assert.That(fallbackImageProduct.Image, Is.Not.Null);
            Assert.That(
                fallbackImageProduct.Image!.Url,
                Is.EqualTo(fallbackImage.Url));
            Assert.That(noImageProduct.Image, Is.Null);
        });
    }

}
