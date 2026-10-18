using ECommerce.Application.Features.Catalog.Products.UpdateProduct;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Persistence.Repositories;
using ECommerce.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Tests.Infrastructure.Persistence;

public sealed class UpdateProductPersistenceTests : PostgresTestBase
{
    [Test]
    public async Task UpdateProduct_should_persist_all_product_details()
    {
        var originalBrand = Brand.Create($"Original brand {Guid.NewGuid():N}");
        var updatedBrand = Brand.Create($"Updated brand {Guid.NewGuid():N}");
        var originalCategory = Category.Create($"Original category {Guid.NewGuid():N}");
        var updatedCategory = Category.Create($"Updated category {Guid.NewGuid():N}");
        var product = Product.Create(
            "Original product",
            "Original description",
            originalBrand.Id,
            originalCategory.Id);

        await using (var arrangeContext = CreateDbContext())
        {
            arrangeContext.Brands.AddRange(originalBrand, updatedBrand);
            arrangeContext.Categories.AddRange(originalCategory, updatedCategory);
            arrangeContext.Products.Add(product);
            await arrangeContext.SaveChangesAsync();
        }

        await using (var updateContext = CreateDbContext())
        {
            var handler = new UpdateProductCommandHandler(
                new ProductRepository(updateContext),
                new BrandRepository(updateContext),
                new CategoryRepository(updateContext),
                updateContext);

            await handler.Handle(
                new UpdateProductCommand(
                    product.Id,
                    "Updated product",
                    "Updated description",
                    updatedBrand.Id,
                    updatedCategory.Id),
                CancellationToken.None);
        }

        await using var verifyContext = CreateDbContext();
        var updatedProduct = await verifyContext.Products
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == product.Id);

        Assert.Multiple(() =>
        {
            Assert.That(updatedProduct.Name, Is.EqualTo("Updated product"));
            Assert.That(updatedProduct.Description, Is.EqualTo("Updated description"));
            Assert.That(updatedProduct.BrandId, Is.EqualTo(updatedBrand.Id));
            Assert.That(updatedProduct.CategoryId, Is.EqualTo(updatedCategory.Id));
        });
    }
}
