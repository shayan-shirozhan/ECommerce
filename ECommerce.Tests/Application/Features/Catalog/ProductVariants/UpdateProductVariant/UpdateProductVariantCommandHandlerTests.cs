using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Catalog.ProductVariants.UpdateProductVariant;
using ECommerce.Domain.Entities;

namespace ECommerce.Tests.Application.Features.Catalog.ProductVariants.UpdateProductVariant;

public sealed class UpdateProductVariantCommandHandlerTests
{
    [Test]
    public async Task Handle_should_update_variant_and_save_changes()
    {
        var variant = ProductVariant.Create(Guid.NewGuid(), "SKU-OLD", "Old name");
        var repository = new FakeProductVariantRepository(variant);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateProductVariantCommandHandler(repository, unitOfWork);

        await handler.Handle(
            new UpdateProductVariantCommand(variant.Id, "SKU-NEW", "New name"),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(variant.SKU, Is.EqualTo("SKU-NEW"));
            Assert.That(variant.Name, Is.EqualTo("New name"));
            Assert.That(repository.CheckedSku, Is.EqualTo("SKU-NEW"));
            Assert.That(repository.ExcludedId, Is.EqualTo(variant.Id));
            Assert.That(unitOfWork.SaveChangesCalled, Is.True);
        });
    }

    [Test]
    public void Handle_should_throw_not_found_when_variant_does_not_exist()
    {
        var handler = new UpdateProductVariantCommandHandler(
            new FakeProductVariantRepository(null),
            new FakeUnitOfWork());

        Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new UpdateProductVariantCommand(Guid.NewGuid(), "SKU-NEW", null),
            CancellationToken.None));
    }

    [Test]
    public void Handle_should_throw_business_rule_when_sku_belongs_to_another_variant()
    {
        var variant = ProductVariant.Create(Guid.NewGuid(), "SKU-OLD", "Old name");
        var repository = new FakeProductVariantRepository(variant) { SkuAlreadyExists = true };
        var unitOfWork = new FakeUnitOfWork();
        var handler = new UpdateProductVariantCommandHandler(repository, unitOfWork);

        Assert.ThrowsAsync<BusinessRuleException>(() => handler.Handle(
            new UpdateProductVariantCommand(variant.Id, "SKU-TAKEN", "New name"),
            CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(variant.SKU, Is.EqualTo("SKU-OLD"));
            Assert.That(variant.Name, Is.EqualTo("Old name"));
            Assert.That(unitOfWork.SaveChangesCalled, Is.False);
        });
    }

    [Test]
    public async Task Handle_should_allow_variant_to_keep_its_current_sku()
    {
        var variant = ProductVariant.Create(Guid.NewGuid(), "SKU-SAME", "Old name");
        var handler = new UpdateProductVariantCommandHandler(
            new FakeProductVariantRepository(variant),
            new FakeUnitOfWork());

        await handler.Handle(
            new UpdateProductVariantCommand(variant.Id, "SKU-SAME", "New name"),
            CancellationToken.None);

        Assert.That(variant.Name, Is.EqualTo("New name"));
    }

    private sealed class FakeProductVariantRepository(ProductVariant? variant)
        : IProductVariantRepository
    {
        public bool SkuAlreadyExists { get; set; }
        public string? CheckedSku { get; private set; }
        public Guid? ExcludedId { get; private set; }

        public Task<bool> ExistsAsync(
            Guid productVariantId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(variant?.Id == productVariantId);

        public Task<bool> ExistsBySkuAsync(
            string sku,
            Guid? exceptProductVariantId = null,
            CancellationToken cancellationToken = default)
        {
            CheckedSku = sku;
            ExcludedId = exceptProductVariantId;
            return Task.FromResult(SkuAlreadyExists);
        }

        public Task AddAsync(
            ProductVariant productVariant,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<ProductVariant?> GetByIdWithDetailsAsync(
            Guid productVariantId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ProductVariant?>(null);

        public Task<ProductVariant?> GetByIdForUpdateAsync(
            Guid productVariantId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(variant?.Id == productVariantId ? variant : null);
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public bool SaveChangesCalled { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalled = true;
            return Task.FromResult(1);
        }

        public Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default) =>
            operation(cancellationToken);
    }
}
