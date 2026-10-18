using System.Reflection;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Catalog.ProductImages.GetProductImageById;
using ECommerce.Application.Features.Catalog.Products.GetProductById;
using ECommerce.Application.Features.Catalog.ProductVariants.GetProductVariantById;
using ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.GetSellerOfferById;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;

namespace ECommerce.Tests.Application.Features.Catalog.PublicRead;

public sealed class PublicCatalogImageVisibilityTests
{
    [Test]
    public async Task GetProductById_should_hide_product_when_status_is_not_active()
    {
        var graph = CreateProductGraph(ProductStatus.Draft);
        var handler = new GetProductByIdQueryHandler(new FakeProductRepository(graph.Product));

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await handler.Handle(new GetProductByIdQuery(graph.Product.Id), CancellationToken.None));
    }

    [Test]
    public async Task GetProductVariantById_should_hide_variant_when_variant_is_not_active()
    {
        var graph = CreateProductGraph();
        var variant = AddVariant(graph.Product, ProductVariantStatus.Draft, "SKU-DRAFT");
        var handler = new GetProductVariantByIdQueryHandler(new FakeProductVariantRepository(variant));

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await handler.Handle(new GetProductVariantByIdQuery(variant.Id), CancellationToken.None));
    }

    [Test]
    public async Task GetProductVariantById_should_hide_variant_when_parent_product_is_not_active()
    {
        var graph = CreateProductGraph(ProductStatus.Inactive);
        var variant = AddVariant(graph.Product, ProductVariantStatus.Active, "SKU-ACTIVE");
        var handler = new GetProductVariantByIdQueryHandler(new FakeProductVariantRepository(variant));

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await handler.Handle(new GetProductVariantByIdQuery(variant.Id), CancellationToken.None));
    }

    [Test]
    public async Task GetSellerOfferById_should_hide_offer_when_offer_is_not_active()
    {
        var graph = CreateProductGraph();
        var variant = AddVariant(graph.Product, ProductVariantStatus.Active, "SKU-ACTIVE");
        var offer = AddOffer(variant, isActive: false);
        var handler = new GetSellerOfferByIdQueryHandler(new FakeSellerOfferRepository(offer));

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await handler.Handle(new GetSellerOfferByIdQuery(offer.Id), CancellationToken.None));
    }

    [Test]
    public async Task GetSellerOfferById_should_hide_offer_when_variant_is_not_active()
    {
        var graph = CreateProductGraph();
        var variant = AddVariant(graph.Product, ProductVariantStatus.Inactive, "SKU-INACTIVE");
        var offer = AddOffer(variant, isActive: true);
        var handler = new GetSellerOfferByIdQueryHandler(new FakeSellerOfferRepository(offer));

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await handler.Handle(new GetSellerOfferByIdQuery(offer.Id), CancellationToken.None));
    }

    [Test]
    public async Task GetSellerOfferById_should_hide_offer_when_product_is_not_active()
    {
        var graph = CreateProductGraph(ProductStatus.Draft);
        var variant = AddVariant(graph.Product, ProductVariantStatus.Active, "SKU-ACTIVE");
        var offer = AddOffer(variant, isActive: true);
        var handler = new GetSellerOfferByIdQueryHandler(new FakeSellerOfferRepository(offer));

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await handler.Handle(new GetSellerOfferByIdQuery(offer.Id), CancellationToken.None));
    }

    [Test]
    public async Task GetProductImageById_should_hide_image_when_product_is_not_active()
    {
        var graph = CreateProductGraph(ProductStatus.Draft);
        var image = AddImage(graph.Product, "draft-product.jpg");
        SetProperty(image, nameof(ProductImage.Product), graph.Product);

        var handler = new GetProductImageByIdQueryHandler(new FakeProductImageRepository(image));

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await handler.Handle(new GetProductImageByIdQuery(image.Id), CancellationToken.None));
    }

    [Test]
    public async Task GetProductImageById_should_hide_variant_image_when_variant_is_not_active()
    {
        var graph = CreateProductGraph();
        var variant = AddVariant(graph.Product, ProductVariantStatus.Inactive, "SKU-INACTIVE");
        var image = AddImage(graph.Product, "inactive-variant.jpg", variant);

        var handler = new GetProductImageByIdQueryHandler(new FakeProductImageRepository(image));

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await handler.Handle(new GetProductImageByIdQuery(image.Id), CancellationToken.None));
    }

    [Test]
    public async Task GetProductImageById_should_return_image_for_active_product_and_variant()
    {
        var graph = CreateProductGraph();
        var variant = AddVariant(graph.Product, ProductVariantStatus.Active, "SKU-ACTIVE");
        var image = AddImage(graph.Product, "active-variant.jpg", variant);

        var handler = new GetProductImageByIdQueryHandler(new FakeProductImageRepository(image));

        var result = await handler.Handle(new GetProductImageByIdQuery(image.Id), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Id, Is.EqualTo(image.Id));
            Assert.That(result.Url, Is.EqualTo("active-variant.jpg"));
            Assert.That(result.ProductVariantId, Is.EqualTo(variant.Id));
        });
    }

    [Test]
    public async Task GetProductById_should_exclude_images_for_inactive_variants_but_keep_product_images()
    {
        var graph = CreateProductGraph();
        var activeVariant = AddVariant(graph.Product, ProductVariantStatus.Active, "SKU-ACTIVE");
        var inactiveVariant = AddVariant(graph.Product, ProductVariantStatus.Inactive, "SKU-INACTIVE");

        var productImage = AddImage(graph.Product, "product.jpg");
        var activeVariantImage = AddImage(graph.Product, "active-variant.jpg", activeVariant);
        _ = AddImage(graph.Product, "inactive-variant.jpg", inactiveVariant);

        var handler = new GetProductByIdQueryHandler(new FakeProductRepository(graph.Product));

        var result = await handler.Handle(new GetProductByIdQuery(graph.Product.Id), CancellationToken.None);
        var returnedImageIds = result.Images.Select(image => image.Id).ToHashSet();

        Assert.Multiple(() =>
        {
            Assert.That(returnedImageIds, Does.Contain(productImage.Id));
            Assert.That(returnedImageIds, Does.Contain(activeVariantImage.Id));
            Assert.That(result.Images, Has.Count.EqualTo(2));
            Assert.That(result.Variants.Select(variant => variant.Id), Is.EquivalentTo(new[] { activeVariant.Id }));
        });
    }

    private static ProductGraph CreateProductGraph(ProductStatus productStatus = ProductStatus.Active)
    {
        var brand = Brand.Create("Test brand");
        var category = Category.Create("Test category");
        var product = Product.Create("Test product", "Test description", brand.Id, category.Id);

        SetProperty(product, nameof(Product.Status), productStatus);
        SetProperty(product, nameof(Product.Brand), brand);
        SetProperty(product, nameof(Product.Category), category);

        return new ProductGraph(product, brand, category);
    }

    private static ProductVariant AddVariant(Product product, ProductVariantStatus status, string sku)
    {
        var variant = ProductVariant.Create(product.Id, sku, sku);
        SetProperty(variant, nameof(ProductVariant.Status), status);
        SetProperty(variant, nameof(ProductVariant.Product), product);
        GetCollection<ProductVariant>(product, "_variants").Add(variant);
        return variant;
    }

    private static SellerOffer AddOffer(ProductVariant variant, bool isActive)
    {
        var offer = SellerOffer.Create(Guid.NewGuid(), variant.Id, 10m, 5);
        if (isActive)
            offer.Activate();

        SetProperty(offer, nameof(SellerOffer.ProductVariant), variant);
        GetCollection<SellerOffer>(variant, "_sellerOffers").Add(offer);
        return offer;
    }

    private static ProductImage AddImage(Product product, string url, ProductVariant? variant = null)
    {
        var image = ProductImage.Create(product.Id, url, null, GetCollection<ProductImage>(product, "_images").Count,
            isPrimary: variant is null, productVariantId: variant?.Id);
        SetProperty(image, nameof(ProductImage.Product), product);
        if (variant is not null)
            SetProperty(image, nameof(ProductImage.ProductVariant), variant);
        GetCollection<ProductImage>(product, "_images").Add(image);
        return image;
    }

    private static List<T> GetCollection<T>(object target, string fieldName)
    {
        var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Expected private collection field '{fieldName}'.");
        return (List<T>)field!.GetValue(target)!;
    }

    private static void SetProperty<T>(object target, string propertyName, T value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(property, Is.Not.Null, $"Expected property '{propertyName}'.");
        property!.SetValue(target, value);
    }

    private sealed record ProductGraph(Product Product, Brand Brand, Category Category);

    private sealed class FakeProductImageRepository(ProductImage? image) : IProductImageRepository
    {
        public Task AddAsync(ProductImage productImage, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<ProductImage?> GetByIdAsync(Guid productImageId, CancellationToken cancellationToken = default) =>
            Task.FromResult(image?.Id == productImageId ? image : null);
    }

    private sealed class FakeProductVariantRepository(ProductVariant? variant) : IProductVariantRepository
    {
        public Task<bool> ExistsAsync(Guid productVariantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(variant?.Id == productVariantId);

        public Task AddAsync(ProductVariant productVariant, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<ProductVariant?> GetByIdWithDetailsAsync(Guid productVariantId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(variant?.Id == productVariantId ? variant : null);

        public Task<ProductVariant?> GetByIdForUpdateAsync(Guid productVariantId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(variant?.Id == productVariantId ? variant : null);
    }

    private sealed class FakeSellerOfferRepository(SellerOffer? offer) : ISellerOfferRepository
    {
        public Task AddAsync(SellerOffer sellerOffer, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<SellerOffer?> GetByIdAsync(Guid sellerOfferId, CancellationToken cancellationToken = default) =>
            Task.FromResult(offer?.Id == sellerOfferId ? offer : null);

        public Task<SellerOffer?> GetByIdForUpdateAsync(Guid sellerOfferId, uint expectedRowVersion,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(offer?.Id == sellerOfferId ? offer : null);
    }

    private sealed class FakeProductRepository(Product? product) : IProductRepository
    {
        public Task<IReadOnlyList<Product>> GetActivePageAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Product> activeProducts =
                product is { Status: ProductStatus.Active } activeProduct
                    ? new[] { activeProduct }
                    : Array.Empty<Product>();

            return Task.FromResult(activeProducts);
        }

        public Task<int> CountActiveAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                product?.Status == ProductStatus.Active ? 1 : 0);
        }

        public Task<Product?> GetByIdWithDetailsAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(product?.Id == productId ? product : null);

        public Task<Product?> GetByIdForUpdateAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(product?.Id == productId ? product : null);

        public Task AddAsync(Product value, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ExistsAsync(Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult(product?.Id == productId);
    }
}
