using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Features.Catalog.Products.GetProducts;

namespace ECommerce.Tests.Application.Features.Catalog.Products.GetProducts;

public sealed class GetProductsQueryHandlerTests
{
    [Test]
    public async Task Handle_should_return_filtered_products_and_metadata()
    {
        var productId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);

        var listingItem = new ProductListingItem(
            productId,
            "Test product",
            "Test description",
            brandId,
            "Test brand",
            categoryId,
            "Test category",
            createdAt,
            150m);

        var repository = new FakeProductRepository(
            new[] { listingItem },
            totalCount: 7);

        var handler = new GetProductsQueryHandler(repository);

        var query = new GetProductsQuery(
            Page: 2,
            PageSize: 5,
            CategoryId: categoryId,
            BrandId: brandId,
            MinPrice: 100m,
            MaxPrice: 200m,
            Search: "  phone  ",
            SortBy: "PRICE-DESC");

        var response = await handler.Handle(
            query,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(response.Page, Is.EqualTo(2));
            Assert.That(response.PageSize, Is.EqualTo(5));
            Assert.That(response.TotalCount, Is.EqualTo(7));
            Assert.That(response.Items, Has.Count.EqualTo(1));

            Assert.That(response.Items[0].Id, Is.EqualTo(productId));
            Assert.That(response.Items[0].Name, Is.EqualTo("Test product"));
            Assert.That(
                response.Items[0].Description,
                Is.EqualTo("Test description"));
            Assert.That(response.Items[0].Brand.Id, Is.EqualTo(brandId));
            Assert.That(response.Items[0].Brand.Name, Is.EqualTo("Test brand"));
            Assert.That(
                response.Items[0].Category.Id,
                Is.EqualTo(categoryId));
            Assert.That(
                response.Items[0].Category.Name,
                Is.EqualTo("Test category"));
            Assert.That(response.Items[0].CreatedAt, Is.EqualTo(createdAt));
            Assert.That(response.Items[0].Price, Is.EqualTo(150m));

            Assert.That(repository.RequestedPage, Is.EqualTo(2));
            Assert.That(repository.RequestedPageSize, Is.EqualTo(5));
            Assert.That(repository.RequestedFilter, Is.Not.Null);
            Assert.That(
                repository.RequestedFilter!.CategoryId,
                Is.EqualTo(categoryId));
            Assert.That(
                repository.RequestedFilter.BrandId,
                Is.EqualTo(brandId));
            Assert.That(
                repository.RequestedFilter.MinPrice,
                Is.EqualTo(100m));
            Assert.That(
                repository.RequestedFilter.MaxPrice,
                Is.EqualTo(200m));
            Assert.That(
                repository.RequestedFilter.Search,
                Is.EqualTo("phone"));
            Assert.That(
                repository.RequestedFilter.SortOrder,
                Is.EqualTo(ProductSortOrder.PriceDescending));
        });
    }

    [Test]
    public async Task Handle_should_return_empty_items_when_page_has_no_products()
    {
        var repository = new FakeProductRepository(
            Array.Empty<ProductListingItem>(),
            totalCount: 12);

        var handler = new GetProductsQueryHandler(repository);

        var response = await handler.Handle(
            new GetProductsQuery(Page: 3, PageSize: 5),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(response.Items, Is.Empty);
            Assert.That(response.Page, Is.EqualTo(3));
            Assert.That(response.PageSize, Is.EqualTo(5));
            Assert.That(response.TotalCount, Is.EqualTo(12));
        });
    }

    private sealed class FakeProductRepository(
        IReadOnlyList<ProductListingItem> pageProducts,
        int totalCount) : IProductRepository
    {
        public int RequestedPage { get; private set; }

        public int RequestedPageSize { get; private set; }

        public ProductListFilter? RequestedFilter { get; private set; }

        public Task<IReadOnlyList<ProductListingItem>> GetActivePageAsync(
            int page,
            int pageSize,
            ProductListFilter filter,
            CancellationToken cancellationToken = default)
        {
            RequestedPage = page;
            RequestedPageSize = pageSize;
            RequestedFilter = filter;

            return Task.FromResult(pageProducts);
        }

        public Task<int> CountActiveAsync(
            ProductListFilter filter,
            CancellationToken cancellationToken = default)
        {
            RequestedFilter = filter;
            return Task.FromResult(totalCount);
        }

        public Task<ECommerce.Domain.Entities.Product?> GetByIdWithDetailsAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ECommerce.Domain.Entities.Product?>(null);
        }

        public Task<ECommerce.Domain.Entities.Product?> GetByIdForUpdateAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ECommerce.Domain.Entities.Product?>(null);
        }

        public Task AddAsync(
            ECommerce.Domain.Entities.Product product,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
    }
}
