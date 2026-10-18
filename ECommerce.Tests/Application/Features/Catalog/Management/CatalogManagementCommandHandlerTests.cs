using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Catalog.Brands.DeleteBrand;
using ECommerce.Application.Features.Catalog.Brands.UpdateBrand;
using ECommerce.Application.Features.Catalog.Categories.DeleteCategory;
using ECommerce.Application.Features.Catalog.Categories.UpdateCategory;
using ECommerce.Domain.Entities;

namespace ECommerce.Tests.Application.Features.Catalog.Management;

public sealed class CatalogManagementCommandHandlerTests
{
    [Test]
    public async Task UpdateBrand_should_update_name_and_save_changes()
    {
        var brand = Brand.Create("Before");
        var repository = new FakeBrandRepository(brand);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateBrandCommandHandler(repository, unitOfWork);

        await handler.Handle(
            new UpdateBrandCommand(brand.Id, "After"),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(brand.Name, Is.EqualTo("After"));
            Assert.That(unitOfWork.SaveChangesCalled, Is.True);
        });
    }

    [Test]
    public void UpdateBrand_should_throw_not_found_for_missing_brand()
    {
        var handler = new UpdateBrandCommandHandler(
            new FakeBrandRepository(),
            new FakeUnitOfWork());

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await handler.Handle(
                new UpdateBrandCommand(Guid.NewGuid(), "Updated"),
                CancellationToken.None));
    }

    [Test]
    public void DeleteBrand_should_reject_brand_with_active_products()
    {
        var brand = Brand.Create("Samsung");
        var repository = new FakeBrandRepository(brand)
        {
            ActiveProductsExist = true
        };

        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeleteBrandCommandHandler(repository, unitOfWork);

        Assert.ThrowsAsync<BusinessRuleException>(async () =>
            await handler.Handle(
                new DeleteBrandCommand(brand.Id),
                CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(brand.IsDeleted, Is.False);
            Assert.That(unitOfWork.SaveChangesCalled, Is.False);
        });
    }

    [Test]
    public async Task DeleteBrand_should_soft_delete_when_no_active_products_exist()
    {
        var brand = Brand.Create("Samsung");
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeleteBrandCommandHandler(
            new FakeBrandRepository(brand),
            unitOfWork);

        await handler.Handle(
            new DeleteBrandCommand(brand.Id),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(brand.IsDeleted, Is.True);
            Assert.That(unitOfWork.SaveChangesCalled, Is.True);
        });
    }

    [Test]
    public async Task UpdateCategory_should_update_name_and_parent()
    {
        var parent = Category.Create("Parent");
        var category = Category.Create("Before");
        var repository = new FakeCategoryRepository(category, parent);
        var unitOfWork = new FakeUnitOfWork();

        var handler = new UpdateCategoryCommandHandler(
            repository,
            unitOfWork);

        await handler.Handle(
            new UpdateCategoryCommand(
                category.Id,
                "After",
                parent.Id),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(category.Name, Is.EqualTo("After"));
            Assert.That(category.ParentCategoryId, Is.EqualTo(parent.Id));
            Assert.That(unitOfWork.SaveChangesCalled, Is.True);
        });
    }

    [Test]
    public void UpdateCategory_should_reject_missing_parent()
    {
        var category = Category.Create("Child");
        var repository = new FakeCategoryRepository(category);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateCategoryCommandHandler(
            repository,
            unitOfWork);

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await handler.Handle(
                new UpdateCategoryCommand(
                    category.Id,
                    "Updated",
                    Guid.NewGuid()),
                CancellationToken.None));

        Assert.That(unitOfWork.SaveChangesCalled, Is.False);
    }

    [Test]
    public void UpdateCategory_should_reject_cycle()
    {
        var parent = Category.Create("Parent");
        var category = Category.Create("Child");
        var repository = new FakeCategoryRepository(category, parent)
        {
            CycleWouldOccur = true
        };

        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateCategoryCommandHandler(
            repository,
            unitOfWork);

        Assert.ThrowsAsync<BusinessRuleException>(async () =>
            await handler.Handle(
                new UpdateCategoryCommand(
                    category.Id,
                    "Updated",
                    parent.Id),
                CancellationToken.None));

        Assert.That(unitOfWork.SaveChangesCalled, Is.False);
    }

    [Test]
    public void DeleteCategory_should_reject_category_with_active_products()
    {
        var category = Category.Create("Phones");
        var repository = new FakeCategoryRepository(category)
        {
            ActiveProductsExist = true
        };

        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeleteCategoryCommandHandler(
            repository,
            unitOfWork);

        Assert.ThrowsAsync<BusinessRuleException>(async () =>
            await handler.Handle(
                new DeleteCategoryCommand(category.Id),
                CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(category.IsDeleted, Is.False);
            Assert.That(unitOfWork.SaveChangesCalled, Is.False);
        });
    }

    [Test]
    public void DeleteCategory_should_reject_category_with_children()
    {
        var category = Category.Create("Phones");
        var repository = new FakeCategoryRepository(category)
        {
            HasChildren = true
        };

        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeleteCategoryCommandHandler(
            repository,
            unitOfWork);

        Assert.ThrowsAsync<BusinessRuleException>(async () =>
            await handler.Handle(
                new DeleteCategoryCommand(category.Id),
                CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(category.IsDeleted, Is.False);
            Assert.That(unitOfWork.SaveChangesCalled, Is.False);
        });
    }

    [Test]
    public async Task DeleteCategory_should_soft_delete_when_no_products_or_children_exist()
    {
        var category = Category.Create("Phones");
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeleteCategoryCommandHandler(
            new FakeCategoryRepository(category),
            unitOfWork);

        await handler.Handle(
            new DeleteCategoryCommand(category.Id),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(category.IsDeleted, Is.True);
            Assert.That(unitOfWork.SaveChangesCalled, Is.True);
        });
    }

    private sealed class FakeBrandRepository(params Brand[] brands)
        : IBrandRepository
    {
        private readonly List<Brand> _brands = brands.ToList();

        public bool ActiveProductsExist { get; set; }

        public Task<bool> ExistsAsync(
            Guid brandId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _brands.Any(brand =>
                    brand.Id == brandId && !brand.IsDeleted));
        }

        public Task AddAsync(
            Brand brand,
            CancellationToken cancellationToken = default)
        {
            _brands.Add(brand);
            return Task.CompletedTask;
        }

        public Task<Brand?> GetByIdAsync(
            Guid brandId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _brands.FirstOrDefault(brand =>
                    brand.Id == brandId && !brand.IsDeleted));
        }

        public Task<Brand?> GetByIdForUpdateAsync(
            Guid brandId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _brands.FirstOrDefault(brand =>
                    brand.Id == brandId && !brand.IsDeleted));
        }

        public Task<bool> HasActiveProductsAsync(
            Guid brandId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ActiveProductsExist);
        }
    }

    private sealed class FakeCategoryRepository(params Category[] categories)
        : ICategoryRepository
    {
        private readonly List<Category> _categories = categories.ToList();

        public bool ActiveProductsExist { get; set; }

        public bool HasChildren { get; set; }

        public bool CycleWouldOccur { get; set; }

        public Task<bool> ExistsAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _categories.Any(category =>
                    category.Id == categoryId && !category.IsDeleted));
        }

        public Task AddAsync(
            Category category,
            CancellationToken cancellationToken = default)
        {
            _categories.Add(category);
            return Task.CompletedTask;
        }

        public Task<Category?> GetByIdAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _categories.FirstOrDefault(category =>
                    category.Id == categoryId && !category.IsDeleted));
        }

        public Task<Category?> GetByIdForUpdateAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _categories.FirstOrDefault(category =>
                    category.Id == categoryId && !category.IsDeleted));
        }

        public Task<bool> HasActiveProductsAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ActiveProductsExist);
        }

        public Task<bool> HasChildrenAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HasChildren);
        }

        public Task<bool> WouldCreateCycleAsync(
            Guid categoryId,
            Guid? proposedParentCategoryId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CycleWouldOccur);
        }
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public bool SaveChangesCalled { get; private set; }

        public Task<int> SaveChangesAsync(
            CancellationToken cancellationToken = default)
        {
            SaveChangesCalled = true;
            return Task.FromResult(1);
        }
    }
}
