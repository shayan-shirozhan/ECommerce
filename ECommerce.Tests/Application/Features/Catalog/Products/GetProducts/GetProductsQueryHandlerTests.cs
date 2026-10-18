using System.Reflection;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Features.Catalog.Products.GetProducts;
using ECommerce.Domain.Entities;

namespace ECommerce.Tests.Application.Features.Catalog.Products.GetProducts;

public sealed class GetProductsQueryHandlerTests
{
    [Test]
    public async Task Handle_should_return_paginated_products_and_metadata()
    {
        var product = CreateActiveProduct();
        var repository = new FakeProductRepository(
            new[] { product },
            totalCount: 7);

        var handler = new GetProductsQueryHandler(repository);

        var response = await handler.Handle(
            new GetProductsQuery(2, 5),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(response.Page, Is.EqualTo(2));
            Assert.That(response.PageSize, Is.EqualTo(5));
            Assert.That(response.TotalCount, Is.EqualTo(7));
            Assert.That(response.Items, Has.Count.EqualTo(1));

            Assert.That(response.Items[0].Id, Is.EqualTo(product.Id));
            Assert.That(response.Items[0].Name, Is.EqualTo(product.Name));
            Assert.That(
                response.Items[0].Description,
                Is.EqualTo(product.Description));
            Assert.That(
                response.Items[0].Brand.Name,
                Is.EqualTo("Test brand"));
            Assert.That(
                response.Items[0].Category.Name,
                Is.EqualTo("Test category"));
            Assert.That(
                response.Items[0].CreatedAt,
                Is.EqualTo(product.CreatedAt));

            Assert.That(repository.RequestedPage, Is.EqualTo(2));
            Assert.That(repository.RequestedPageSize, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task Handle_should_return_empty_items_when_page_has_no_products()
    {
        var repository = new FakeProductRepository(
            Array.Empty<Product>(),
            totalCount: 12);

        var handler = new GetProductsQueryHandler(repository);

        var response = await handler.Handle(
            new GetProductsQuery(3, 5),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(response.Items, Is.Empty);
            Assert.That(response.Page, Is.EqualTo(3));
            Assert.That(response.PageSize, Is.EqualTo(5));
            Assert.That(response.TotalCount, Is.EqualTo(12));
        });
    }

    private static Product CreateActiveProduct()
    {
        var brand = Brand.Create("Test brand");
        var category = Category.Create("Test category");

        var product = Product.Create(
            "Test product",
            "Test description",
            brand.Id,
            category.Id);

        product.Activate();

        SetProperty(product, nameof(Product.Brand), brand);
        SetProperty(product, nameof(Product.Category), category);

        return product;
    }

    private static void SetProperty<T>(
        object target,
        string propertyName,
        T value)
    {
        var property = target.GetType().GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public);

        Assert.That(
            property,
            Is.Not.Null,
            $"Expected property '{propertyName}'.");

        property!.SetValue(target, value);
    }

    private sealed class FakeProductRepository(
        IReadOnlyList<Product> pageProducts,
        int totalCount) : IProductRepository
    {
        public int RequestedPage { get; private set; }

        public int RequestedPageSize { get; private set; }

        public Task<Product?> GetByIdWithDetailsAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Product?>(
                pageProducts.FirstOrDefault(product => product.Id == productId));
        }

        public Task<IReadOnlyList<Product>> GetActivePageAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            RequestedPage = page;
            RequestedPageSize = pageSize;

            return Task.FromResult(pageProducts);
        }

        public Task<int> CountActiveAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(totalCount);
        }

        public Task<Product?> GetByIdForUpdateAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Product?>(
                pageProducts.FirstOrDefault(product => product.Id == productId));
        }

        public Task AddAsync(
            Product product,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(
            Guid productId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                pageProducts.Any(product => product.Id == productId));
        }
    }
}
