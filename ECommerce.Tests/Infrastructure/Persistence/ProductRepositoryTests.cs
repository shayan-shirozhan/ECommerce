using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Persistence.Repositories;
using ECommerce.Tests.Infrastructure.Fixtures;

namespace ECommerce.Tests.Infrastructure.Persistence;

public sealed class ProductRepositoryTests : PostgresTestBase
{
    [Test]
    public async Task GetActivePageAsync_should_return_only_active_products_and_count_them()
    {
        await using var dbContext = CreateDbContext();

        var brand = Brand.Create("Pagination test brand");
        var category = Category.Create("Pagination test category");

        var firstActiveProduct = Product.Create(
            "First active product",
            "First active product description",
            brand.Id,
            category.Id);
        firstActiveProduct.Activate();

        var secondActiveProduct = Product.Create(
            "Second active product",
            "Second active product description",
            brand.Id,
            category.Id);
        secondActiveProduct.Activate();

        var inactiveProduct = Product.Create(
            "Inactive product",
            "Inactive product description",
            brand.Id,
            category.Id);
        inactiveProduct.Deactivate();

        dbContext.Brands.Add(brand);
        dbContext.Categories.Add(category);

        dbContext.Products.AddRange(
            firstActiveProduct,
            secondActiveProduct,
            inactiveProduct);

        await dbContext.SaveChangesAsync();

        var repository = new ProductRepository(dbContext);

        var totalCount = await repository.CountActiveAsync(
            CancellationToken.None);

        var firstPage = await repository.GetActivePageAsync(
            1,
            1,
            CancellationToken.None);

        var secondPage = await repository.GetActivePageAsync(
            2,
            1,
            CancellationToken.None);

        var beyondLastPage = await repository.GetActivePageAsync(
            3,
            1,
            CancellationToken.None);

        var pagedProducts = firstPage.Concat(secondPage).ToList();
        var pagedProductIds = pagedProducts
            .Select(product => product.Id)
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(totalCount, Is.EqualTo(2));

            Assert.That(firstPage, Has.Count.EqualTo(1));
            Assert.That(secondPage, Has.Count.EqualTo(1));
            Assert.That(beyondLastPage, Is.Empty);

            Assert.That(
                pagedProductIds,
                Is.EquivalentTo(new[]
                {
                    firstActiveProduct.Id,
                    secondActiveProduct.Id
                }));

            Assert.That(
                pagedProductIds.Distinct().Count(),
                Is.EqualTo(2));

            Assert.That(
                pagedProducts.All(product =>
                    product.Status == ProductStatus.Active),
                Is.True);

            Assert.That(firstPage[0].Brand, Is.Not.Null);
            Assert.That(firstPage[0].Category, Is.Not.Null);
            Assert.That(secondPage[0].Brand, Is.Not.Null);
            Assert.That(secondPage[0].Category, Is.Not.Null);
        });
    }
}
