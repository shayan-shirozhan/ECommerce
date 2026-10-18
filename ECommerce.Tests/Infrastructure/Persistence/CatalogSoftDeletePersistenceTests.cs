using ECommerce.Application.Exceptions;
using ECommerce.Infrastructure.Persistence.Repositories;
using ECommerce.Domain.Entities;
using ECommerce.Tests.Infrastructure.Fixtures;

namespace ECommerce.Tests.Infrastructure.Persistence;

public sealed class CatalogSoftDeletePersistenceTests : PostgresTestBase
{
    [Test]
    public async Task Soft_deleted_brand_should_be_hidden_and_name_reused()
    {
        await using var dbContext = CreateDbContext();

        var name = $"Brand-{Guid.NewGuid():N}";
        var brand = Brand.Create(name);

        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync();

        var repository = new BrandRepository(dbContext);

        var trackedBrand = await repository.GetByIdForUpdateAsync(brand.Id);

        Assert.That(trackedBrand, Is.Not.Null);

        trackedBrand!.SoftDelete();
        await dbContext.SaveChangesAsync();

        Assert.That(
            await repository.ExistsAsync(brand.Id),
            Is.False);

        Assert.That(
            await repository.GetByIdAsync(brand.Id),
            Is.Null);

        var replacementBrand = Brand.Create(name);

        await repository.AddAsync(replacementBrand);
        await dbContext.SaveChangesAsync();

        Assert.That(
            await repository.ExistsAsync(replacementBrand.Id),
            Is.True);
    }

    [Test]
    public async Task Soft_deleted_root_category_should_allow_name_reuse()
    {
        await using var dbContext = CreateDbContext();

        var name = $"Category-{Guid.NewGuid():N}";
        var category = Category.Create(name);

        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();

        var repository = new CategoryRepository(dbContext);

        var trackedCategory =
            await repository.GetByIdForUpdateAsync(category.Id);

        Assert.That(trackedCategory, Is.Not.Null);

        trackedCategory!.SoftDelete();
        await dbContext.SaveChangesAsync();

        Assert.That(
            await repository.ExistsAsync(category.Id),
            Is.False);

        Assert.That(
            await repository.GetByIdAsync(category.Id),
            Is.Null);

        var replacementCategory = Category.Create(name);

        await repository.AddAsync(replacementCategory);
        await dbContext.SaveChangesAsync();

        Assert.That(
            await repository.ExistsAsync(replacementCategory.Id),
            Is.True);
    }

    [Test]
    public async Task Database_should_reject_duplicate_non_deleted_brand_names()
    {
        await using var dbContext = CreateDbContext();

        var name = $"Duplicate-{Guid.NewGuid():N}";

        dbContext.Brands.Add(Brand.Create(name));
        await dbContext.SaveChangesAsync();

        dbContext.Brands.Add(Brand.Create(name));

        Assert.ThrowsAsync<PersistenceConflictException>(async () =>
        {
            await dbContext.SaveChangesAsync();
        });
    }

    [Test]
    public async Task Database_should_reject_duplicate_non_deleted_root_category_names()
    {
        await using var dbContext = CreateDbContext();

        var name = $"Duplicate-{Guid.NewGuid():N}";

        dbContext.Categories.Add(Category.Create(name));
        await dbContext.SaveChangesAsync();

        dbContext.Categories.Add(Category.Create(name));

        Assert.ThrowsAsync<PersistenceConflictException>(async () =>
        {
            await dbContext.SaveChangesAsync();
        });
    }
}
