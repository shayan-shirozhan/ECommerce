using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Persistence.Repositories;
using ECommerce.Tests.Infrastructure.Fixtures;

namespace ECommerce.Tests.Infrastructure.Persistence;

public sealed class CatalogListingRepositoryTests : PostgresTestBase
{
    [Test]
    public async Task GetAllBrands_should_return_non_deleted_brands_sorted_by_name()
    {
        await using var dbContext = CreateDbContext();

        var zulu = Brand.Create($"Zulu-{Guid.NewGuid():N}");
        var alpha = Brand.Create($"Alpha-{Guid.NewGuid():N}");
        var deleted = Brand.Create($"Deleted-{Guid.NewGuid():N}");
        deleted.SoftDelete();

        dbContext.Brands.AddRange(zulu, alpha, deleted);
        await dbContext.SaveChangesAsync();

        var repository = new BrandRepository(dbContext);
        var result = await repository.GetAllAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(
                result.Select(brand => brand.Name),
                Is.EqualTo(new[] { alpha.Name, zulu.Name }));
            Assert.That(
                result.Any(brand => brand.Id == deleted.Id),
                Is.False);
        });
    }

    [Test]
    public async Task GetAllCategories_should_return_non_deleted_categories_with_parent_ids_sorted_by_name()
    {
        await using var dbContext = CreateDbContext();

        var parent = Category.Create($"Parent-{Guid.NewGuid():N}");
        var child = Category.Create(
            $"Child-{Guid.NewGuid():N}",
            parent.Id);
        var deleted = Category.Create($"Deleted-{Guid.NewGuid():N}");
        deleted.SoftDelete();

        dbContext.Categories.AddRange(parent, child, deleted);
        await dbContext.SaveChangesAsync();

        var repository = new CategoryRepository(dbContext);
        var result = await repository.GetAllAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(
                result.Select(category => category.Name),
                Is.EqualTo(new[] { child.Name, parent.Name }));

            var returnedChild = result.Single(category => category.Id == child.Id);
            var returnedParent = result.Single(category => category.Id == parent.Id);

            Assert.That(
                returnedChild.ParentCategoryId,
                Is.EqualTo(parent.Id));
            Assert.That(returnedParent.ParentCategoryId, Is.Null);
            Assert.That(
                result.Any(category => category.Id == deleted.Id),
                Is.False);
        });
    }
}
