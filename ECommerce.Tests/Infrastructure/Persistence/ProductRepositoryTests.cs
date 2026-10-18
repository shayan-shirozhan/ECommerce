using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Persistence.Repositories;
using ECommerce.Tests.Infrastructure.Fixtures;

namespace ECommerce.Tests.Infrastructure.Persistence;

public sealed class ProductRepositoryTests : PostgresTestBase
{
    [Test]
    public async Task Product_queries_should_apply_filters_sorting_and_active_visibility()
    {
        await using var dbContext = CreateDbContext();

        var samsung = Brand.Create("Samsung");
        var apple = Brand.Create("Apple");

        var phones = Category.Create("Phones");
        var accessories = Category.Create("Accessories");

        var sellerId = Guid.NewGuid();

        var samsungPhone = CreateProductWithOffer(
            "Samsung Phone",
            "Samsung smartphone",
            samsung,
            phones,
            100m,
            "SKU-SAMSUNG-PHONE",
            sellerId);

        var samsungTablet = CreateProductWithOffer(
            "Samsung Tablet",
            "Samsung tablet",
            samsung,
            accessories,
            200m,
            "SKU-SAMSUNG-TABLET",
            sellerId);

        var applePhone = CreateProductWithOffer(
            "Apple Phone",
            "Apple smartphone",
            apple,
            phones,
            150m,
            "SKU-APPLE-PHONE",
            sellerId);

        var samsungWithoutOffer = Product.Create(
            "Samsung No Offer",
            "Samsung product without an active offer",
            samsung.Id,
            accessories.Id);

        samsungWithoutOffer.Activate();

        var inactiveProduct = Product.Create(
            "Samsung Inactive",
            "Inactive Samsung product",
            samsung.Id,
            phones.Id);

        inactiveProduct.Deactivate();

        dbContext.Brands.AddRange(samsung, apple);
        dbContext.Categories.AddRange(phones, accessories);

        dbContext.Products.AddRange(
            samsungPhone.Product,
            samsungTablet.Product,
            applePhone.Product,
            samsungWithoutOffer,
            inactiveProduct);

        dbContext.ProductVariants.AddRange(
            samsungPhone.Variant,
            samsungTablet.Variant,
            applePhone.Variant);

        dbContext.SellerOffers.AddRange(
            samsungPhone.Offer,
            samsungTablet.Offer,
            applePhone.Offer);

        await dbContext.SaveChangesAsync();

        var repository = new ProductRepository(dbContext);

        var defaultFilter = new ProductListFilter(
            CategoryId: null,
            BrandId: null,
            MinPrice: null,
            MaxPrice: null,
            Search: null,
            SortOrder: ProductSortOrder.Newest);

        var allProducts = await repository.GetActivePageAsync(
            1,
            10,
            defaultFilter);

        var totalCount = await repository.CountActiveAsync(defaultFilter);

        Assert.Multiple(() =>
        {
            Assert.That(totalCount, Is.EqualTo(4));
            Assert.That(allProducts, Has.Count.EqualTo(4));

            Assert.That(
                allProducts.Select(product => product.Name),
                Is.EquivalentTo(new[]
                {
                    "Samsung Phone",
                    "Samsung Tablet",
                    "Apple Phone",
                    "Samsung No Offer"
                }));

            Assert.That(
                allProducts.Any(product => product.Name == "Samsung Inactive"),
                Is.False);

            Assert.That(
                allProducts.Single(product =>
                    product.Name == "Samsung No Offer").Price,
                Is.Null);
        });

        var brandFilter = defaultFilter with
        {
            BrandId = samsung.Id
        };

        var samsungProducts = await repository.GetActivePageAsync(
            1,
            10,
            brandFilter);

        Assert.That(
            await repository.CountActiveAsync(brandFilter),
            Is.EqualTo(3));

        Assert.That(samsungProducts, Has.Count.EqualTo(3));
        Assert.That(
            samsungProducts.All(product => product.BrandId == samsung.Id),
            Is.True);

        var categoryFilter = defaultFilter with
        {
            CategoryId = phones.Id
        };

        var phoneProducts = await repository.GetActivePageAsync(
            1,
            10,
            categoryFilter);

        Assert.That(
            await repository.CountActiveAsync(categoryFilter),
            Is.EqualTo(2));

        Assert.That(phoneProducts, Has.Count.EqualTo(2));
        Assert.That(
            phoneProducts.All(product => product.CategoryId == phones.Id),
            Is.True);

        var searchFilter = defaultFilter with
        {
            Search = "sAmSuNg"
        };

        var searchedProducts = await repository.GetActivePageAsync(
            1,
            10,
            searchFilter);

        Assert.That(
            await repository.CountActiveAsync(searchFilter),
            Is.EqualTo(3));

        Assert.That(
            searchedProducts.Select(product => product.Name),
            Is.EquivalentTo(new[]
            {
                "Samsung Phone",
                "Samsung Tablet",
                "Samsung No Offer"
            }));

        var priceFilter = defaultFilter with
        {
            MinPrice = 120m,
            MaxPrice = 180m
        };

        var priceFilteredProducts = await repository.GetActivePageAsync(
            1,
            10,
            priceFilter);

        Assert.That(
            await repository.CountActiveAsync(priceFilter),
            Is.EqualTo(1));

        Assert.That(priceFilteredProducts, Has.Count.EqualTo(1));
        Assert.That(priceFilteredProducts[0].Name, Is.EqualTo("Apple Phone"));
        Assert.That(priceFilteredProducts[0].Price, Is.EqualTo(150m));

        var ascendingFilter = defaultFilter with
        {
            SortOrder = ProductSortOrder.PriceAscending
        };

        var ascendingProducts = await repository.GetActivePageAsync(
            1,
            10,
            ascendingFilter);

        Assert.That(
            ascendingProducts.Select(product => product.Price),
            Is.EqualTo(new decimal?[]
            {
                100m,
                150m,
                200m,
                null
            }));

        var descendingFilter = defaultFilter with
        {
            SortOrder = ProductSortOrder.PriceDescending
        };

        var descendingProducts = await repository.GetActivePageAsync(
            1,
            10,
            descendingFilter);

        Assert.That(
            descendingProducts.Select(product => product.Price),
            Is.EqualTo(new decimal?[]
            {
                200m,
                150m,
                100m,
                null
            }));
    }

    private static ProductOfferGraph CreateProductWithOffer(
        string name,
        string description,
        Brand brand,
        Category category,
        decimal price,
        string sku,
        Guid sellerId)
    {
        var product = Product.Create(
            name,
            description,
            brand.Id,
            category.Id);

        product.Activate();

        var variant = ProductVariant.Create(
            product,
            sku,
            $"{name} variant");

        variant.Activate(product);

        var offer = SellerOffer.Create(
            sellerId,
            variant.Id,
            price,
            stock: 10);

        offer.Activate();

        return new ProductOfferGraph(product, variant, offer);
    }

    private sealed record ProductOfferGraph(
        Product Product,
        ProductVariant Variant,
        SellerOffer Offer);
}
