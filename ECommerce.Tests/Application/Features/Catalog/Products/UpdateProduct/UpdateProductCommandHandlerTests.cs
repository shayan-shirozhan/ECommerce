using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Catalog.Products.UpdateProduct;
using ECommerce.Domain.Entities;

namespace ECommerce.Tests.Application.Features.Catalog.Products.UpdateProduct;

public sealed class UpdateProductCommandHandlerTests
{
    [Test]
    public async Task Handle_should_update_product_details_and_save_changes()
    {
        var product = CreateProduct();
        var brandId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(
            new FakeProductRepository(product),
            new FakeBrandRepository(),
            new FakeCategoryRepository(),
            unitOfWork);

        await handler.Handle(
            new UpdateProductCommand(
                product.Id,
                "Updated phone",
                "Updated description",
                brandId,
                categoryId),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(product.Name, Is.EqualTo("Updated phone"));
            Assert.That(product.Description, Is.EqualTo("Updated description"));
            Assert.That(product.BrandId, Is.EqualTo(brandId));
            Assert.That(product.CategoryId, Is.EqualTo(categoryId));
            Assert.That(unitOfWork.SaveChangesCalled, Is.True);
        });
    }

    [Test]
    public void Handle_should_throw_not_found_when_product_does_not_exist()
    {
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(
            new FakeProductRepository(),
            new FakeBrandRepository(),
            new FakeCategoryRepository(),
            unitOfWork);

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await handler.Handle(
                CreateCommand(Guid.NewGuid()),
                CancellationToken.None));

        Assert.That(unitOfWork.SaveChangesCalled, Is.False);
    }

    [Test]
    public void Handle_should_throw_not_found_when_brand_does_not_exist()
    {
        var product = CreateProduct();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(
            new FakeProductRepository(product),
            new FakeBrandRepository(exists: false),
            new FakeCategoryRepository(),
            unitOfWork);

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await handler.Handle(
                CreateCommand(product.Id),
                CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(product.Name, Is.EqualTo("Original phone"));
            Assert.That(unitOfWork.SaveChangesCalled, Is.False);
        });
    }

    [Test]
    public void Handle_should_throw_not_found_when_category_does_not_exist()
    {
        var product = CreateProduct();
        var unitOfWork = new FakeUnitOfWork();
        var handler = CreateHandler(
            new FakeProductRepository(product),
            new FakeBrandRepository(),
            new FakeCategoryRepository(exists: false),
            unitOfWork);

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await handler.Handle(
                CreateCommand(product.Id),
                CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(product.Name, Is.EqualTo("Original phone"));
            Assert.That(unitOfWork.SaveChangesCalled, Is.False);
        });
    }

    private static UpdateProductCommandHandler CreateHandler(
        IProductRepository productRepository,
        IBrandRepository brandRepository,
        ICategoryRepository categoryRepository,
        IUnitOfWork unitOfWork) =>
        new(productRepository, brandRepository, categoryRepository, unitOfWork);

    private static UpdateProductCommand CreateCommand(Guid productId) =>
        new(
            productId,
            "Updated phone",
            "Updated description",
            Guid.NewGuid(),
            Guid.NewGuid());

    private static Product CreateProduct() =>
        Product.Create(
            "Original phone",
            "Original description",
            Guid.NewGuid(),
            Guid.NewGuid());

    private sealed class FakeProductRepository(Product? product = null)
        : IProductRepository
    {
        public Task<Product?> GetByIdWithDetailsAsync(
            Guid productId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Product?>(null);

        public Task<IReadOnlyList<ProductListingItem>> GetActivePageAsync(
            int page,
            int pageSize,
            ProductListFilter filter,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ProductListingItem>>(
                Array.Empty<ProductListingItem>());

        public Task<int> CountActiveAsync(
            ProductListFilter filter,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<Product?> GetByIdForUpdateAsync(
            Guid productId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Product?>(product?.Id == productId ? product : null);

        public Task AddAsync(
            Product product,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> ExistsAsync(
            Guid productId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(product?.Id == productId);
    }

    private sealed class FakeBrandRepository(bool exists = true)
        : IBrandRepository
    {
        public Task<bool> ExistsAsync(
            Guid brandId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(exists);

        public Task AddAsync(
            Brand brand,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Brand?> GetByIdAsync(
            Guid brandId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Brand?>(null);

        public Task<Brand?> GetByIdForUpdateAsync(
            Guid brandId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Brand?>(null);

        public Task<bool> HasActiveProductsAsync(
            Guid brandId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FakeCategoryRepository(bool exists = true)
        : ICategoryRepository
    {
        public Task<bool> ExistsAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(exists);

        public Task AddAsync(
            Category category,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<Category?> GetByIdAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Category?>(null);

        public Task<Category?> GetByIdForUpdateAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Category?>(null);

        public Task<bool> HasActiveProductsAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> HasChildrenAsync(
            Guid categoryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<bool> WouldCreateCycleAsync(
            Guid categoryId,
            Guid? proposedParentCategoryId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
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
