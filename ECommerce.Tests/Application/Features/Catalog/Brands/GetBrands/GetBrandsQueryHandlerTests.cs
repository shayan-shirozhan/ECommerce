using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Features.Catalog.Brands.GetBrands;

namespace ECommerce.Tests.Application.Features.Catalog.Brands.GetBrands;

public sealed class GetBrandsQueryHandlerTests
{
    [Test]
    public async Task Handle_should_map_all_brands_from_repository()
    {
        var firstBrandId = Guid.NewGuid();
        var secondBrandId = Guid.NewGuid();
        var repository = new FakeBrandRepository(
        [
            new BrandListingItem(firstBrandId, "Apple"),
            new BrandListingItem(secondBrandId, "Samsung")
        ]);
        var handler = new GetBrandsQueryHandler(repository);

        var response = await handler.Handle(
            new GetBrandsQuery(),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(response.Items, Has.Count.EqualTo(2));
            Assert.That(response.Items[0].Id, Is.EqualTo(firstBrandId));
            Assert.That(response.Items[0].Name, Is.EqualTo("Apple"));
            Assert.That(response.Items[1].Id, Is.EqualTo(secondBrandId));
            Assert.That(response.Items[1].Name, Is.EqualTo("Samsung"));
        });
    }

    [Test]
    public async Task Handle_should_return_empty_items_when_no_brands_exist()
    {
        var handler = new GetBrandsQueryHandler(
            new FakeBrandRepository([]));

        var response = await handler.Handle(
            new GetBrandsQuery(),
            CancellationToken.None);

        Assert.That(response.Items, Is.Empty);
    }

    private sealed class FakeBrandRepository(
        IReadOnlyList<BrandListingItem> brands) : IBrandRepository
    {
        public Task<IReadOnlyList<BrandListingItem>> GetAllAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(brands);

        public Task<bool> ExistsAsync(
            Guid brandId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task AddAsync(
            ECommerce.Domain.Entities.Brand brand,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<ECommerce.Domain.Entities.Brand?> GetByIdAsync(
            Guid brandId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ECommerce.Domain.Entities.Brand?>(null);

        public Task<ECommerce.Domain.Entities.Brand?> GetByIdForUpdateAsync(
            Guid brandId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ECommerce.Domain.Entities.Brand?>(null);

        public Task<bool> HasActiveProductsAsync(
            Guid brandId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }
}
