using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Catalog.Products.ActivateProduct;
using ECommerce.Application.Features.Catalog.Products.DeactivateProduct;
using ECommerce.Application.Features.Catalog.ProductVariants.ActivateProductVariant;
using ECommerce.Application.Features.Catalog.ProductVariants.DeactivateProductVariant;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Tests.Application.Features.Catalog.Lifecycle;

public sealed class CatalogLifecycleCommandHandlerTests
{
    [Test]
    public async Task ActivateProduct_should_activate_product_and_save_changes()
    {
        var product = CreateProduct();
        var repository = new FakeProductRepository { ProductForUpdate = product };
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ActivateProductCommandHandler(repository, unitOfWork);

        await handler.Handle(new ActivateProductCommand(product.Id), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(product.Status, Is.EqualTo(ProductStatus.Active));
            Assert.That(unitOfWork.SaveChangesCalled, Is.True);
        });
    }

    [Test]
    public void ActivateProduct_should_throw_not_found_when_product_does_not_exist()
    {
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ActivateProductCommandHandler(new FakeProductRepository(), unitOfWork);

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await handler.Handle(new ActivateProductCommand(Guid.NewGuid()), CancellationToken.None));
        Assert.That(unitOfWork.SaveChangesCalled, Is.False);
    }

    [Test]
    public async Task DeactivateProduct_should_deactivate_product_and_save_changes()
    {
        var product = CreateProduct();
        product.Activate();
        var repository = new FakeProductRepository { ProductForUpdate = product };
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeactivateProductCommandHandler(repository, unitOfWork);

        await handler.Handle(new DeactivateProductCommand(product.Id), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(product.Status, Is.EqualTo(ProductStatus.Inactive));
            Assert.That(unitOfWork.SaveChangesCalled, Is.True);
        });
    }

    [Test]
    public async Task ActivateProductVariant_should_activate_variant_when_parent_product_is_active()
    {
        var product = CreateProduct();
        product.Activate();
        var variant = ProductVariant.Create(product, "PHONE-BLK", "Black");
        var repository = new FakeProductVariantRepository { VariantForUpdate = variant };
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ActivateProductVariantCommandHandler(repository, unitOfWork);

        await handler.Handle(new ActivateProductVariantCommand(variant.Id), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(variant.Status, Is.EqualTo(ProductVariantStatus.Active));
            Assert.That(unitOfWork.SaveChangesCalled, Is.True);
        });
    }

    [Test]
    public void ActivateProductVariant_should_reject_variant_when_parent_product_is_inactive()
    {
        var product = CreateProduct();
        var variant = ProductVariant.Create(product, "PHONE-BLK", "Black");
        var repository = new FakeProductVariantRepository { VariantForUpdate = variant };
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ActivateProductVariantCommandHandler(repository, unitOfWork);

        Assert.ThrowsAsync<DomainException>(async () =>
            await handler.Handle(new ActivateProductVariantCommand(variant.Id), CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(variant.Status, Is.EqualTo(ProductVariantStatus.Draft));
            Assert.That(unitOfWork.SaveChangesCalled, Is.False);
        });
    }

    [Test]
    public async Task DeactivateProductVariant_should_deactivate_variant_and_save_changes()
    {
        var product = CreateProduct();
        product.Activate();
        var variant = ProductVariant.Create(product, "PHONE-BLK", "Black");
        variant.Activate(product);
        var repository = new FakeProductVariantRepository { VariantForUpdate = variant };
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeactivateProductVariantCommandHandler(repository, unitOfWork);

        await handler.Handle(new DeactivateProductVariantCommand(variant.Id), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(variant.Status, Is.EqualTo(ProductVariantStatus.Inactive));
            Assert.That(unitOfWork.SaveChangesCalled, Is.True);
        });
    }

    [Test]
    public void DeactivateProductVariant_should_throw_not_found_when_variant_does_not_exist()
    {
        var unitOfWork = new FakeUnitOfWork();
        var handler = new DeactivateProductVariantCommandHandler(new FakeProductVariantRepository(), unitOfWork);

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await handler.Handle(new DeactivateProductVariantCommand(Guid.NewGuid()), CancellationToken.None));
        Assert.That(unitOfWork.SaveChangesCalled, Is.False);
    }

    private static Product CreateProduct() =>
        Product.Create("Phone", "Phone description", Guid.NewGuid(), Guid.NewGuid());

    private sealed class FakeProductRepository : IProductRepository
    {
        public Product? ProductForUpdate { get; set; }

        public Task<Product?> GetByIdWithDetailsAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Product?>(null);

        public Task<IReadOnlyList<Product>> GetActivePageAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var activeProducts =
                ProductForUpdate is { Status: ProductStatus.Active } product
                    ? new[] { product }
                    : Array.Empty<Product>();

            IReadOnlyList<Product> products = activeProducts
                .OrderByDescending(product => product.CreatedAt)
                .ThenBy(product => product.Id)
                .Skip(checked((int)(((long)page - 1L) * pageSize)))
                .Take(pageSize)
                .ToArray();

            return Task.FromResult(products);
        }

        public Task<int> CountActiveAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                ProductForUpdate?.Status == ProductStatus.Active ? 1 : 0);
        }

        public Task<Product?> GetByIdForUpdateAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ProductForUpdate?.Id == productId ? ProductForUpdate : null);

        public Task AddAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ExistsAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(ProductForUpdate?.Id == productId);
    }

    private sealed class FakeProductVariantRepository : IProductVariantRepository
    {
        public ProductVariant? VariantForUpdate { get; set; }

        public Task<bool> ExistsAsync(Guid productVariantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(VariantForUpdate?.Id == productVariantId);

        public Task AddAsync(ProductVariant productVariant, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<ProductVariant?> GetByIdWithDetailsAsync(Guid productVariantId,
            CancellationToken cancellationToken = default) => Task.FromResult<ProductVariant?>(null);

        public Task<ProductVariant?> GetByIdForUpdateAsync(Guid productVariantId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(VariantForUpdate?.Id == productVariantId ? VariantForUpdate : null);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public bool SaveChangesCalled { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalled = true;
            return Task.FromResult(1);
        }
    }
}
