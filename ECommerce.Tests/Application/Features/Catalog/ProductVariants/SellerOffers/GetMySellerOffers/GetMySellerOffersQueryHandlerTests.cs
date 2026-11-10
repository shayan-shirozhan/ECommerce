using ECommerce.Application;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Catalog.ProductVariants.SellerOffers.GetMySellerOffers;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Catalog.ProductVariants.SellerOffers.GetMySellerOffers;

public sealed class GetMySellerOffersQueryHandlerTests : PostgresTestBase
{
    [Test]
    public void Handle_should_reject_request_without_authenticated_user_id()
    {
        using var provider = CreateProvider(null);

        Assert.ThrowsAsync<UnauthorizedException>(
            async () => await SendAsync(provider));
    }

    [Test]
    public async Task Handle_should_return_empty_items_when_seller_has_no_offers()
    {
        var sellerId = Guid.NewGuid();
        using var provider = CreateProvider(sellerId);

        var response = await SendAsync(provider);

        Assert.Multiple(() =>
        {
            Assert.That(response.Items, Is.Empty);
            Assert.That(response.TotalCount, Is.Zero);
            Assert.That(response.Page, Is.EqualTo(1));
            Assert.That(response.PageSize, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task Handle_should_return_only_current_sellers_offers_and_include_inactive_offers()
    {
        // Arrange
        var currentSellerId = Guid.NewGuid();
        var otherSellerId = Guid.NewGuid();

        var olderOffer = await SeedOfferAsync(
            currentSellerId,
            "Older Product",
            "Older Variant",
            100_000m,
            5,
            SellerOfferStatus.Inactive,
            new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));

        var newerOffer = await SeedOfferAsync(
            currentSellerId,
            "Newer Product",
            "Newer Variant",
            200_000m,
            10,
            SellerOfferStatus.Active,
            new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc));

        // This offer must not be visible to the current seller.
        await SeedOfferAsync(
            otherSellerId,
            "Other Seller Product",
            "Other Variant",
            300_000m,
            15,
            SellerOfferStatus.Active,
            new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc));

        using var provider = CreateProvider(currentSellerId);

        // Act
        var response = await SendAsync(provider);

        // Assert
        Assert.That(response.Items, Has.Count.EqualTo(2));
        Assert.That(response.TotalCount, Is.EqualTo(2));

        Assert.Multiple(() =>
        {
            Assert.That(
                response.Items.Select(item => item.Id),
                Is.EqualTo(new[] { newerOffer.Id, olderOffer.Id }));

            Assert.That(
                response.Items[0].ProductId,
                Is.EqualTo(newerOffer.ProductId));

            Assert.That(
                response.Items[0].ProductName,
                Is.EqualTo("Newer Product"));

            Assert.That(
                response.Items[0].ProductVariantId,
                Is.EqualTo(newerOffer.ProductVariantId));

            Assert.That(
                response.Items[0].VariantName,
                Is.EqualTo("Newer Variant"));

            Assert.That(
                response.Items[0].SKU,
                Is.EqualTo(newerOffer.SKU));

            Assert.That(response.Items[0].Price, Is.EqualTo(200_000m));
            Assert.That(response.Items[0].Stock, Is.EqualTo(10));
            Assert.That(
                response.Items[0].Status,
                Is.EqualTo(SellerOfferStatus.Active));

            Assert.That(
                response.Items[1].Status,
                Is.EqualTo(SellerOfferStatus.Inactive));
        });
    }

    [Test]
    public async Task Handle_should_apply_status_filter_and_pagination_consistently()
    {
        // Arrange
        var sellerId = Guid.NewGuid();
        var otherSellerId = Guid.NewGuid();

        var oldestActiveOffer = await SeedOfferAsync(
            sellerId,
            "Oldest Active",
            "Standard",
            100m,
            5,
            SellerOfferStatus.Active,
            new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc));

        await SeedOfferAsync(
            sellerId,
            "Inactive Product",
            "Standard",
            200m,
            10,
            SellerOfferStatus.Inactive,
            new DateTime(2026, 1, 2, 10, 0, 0, DateTimeKind.Utc));

        var newestActiveOffer = await SeedOfferAsync(
            sellerId,
            "Newest Active",
            "Standard",
            300m,
            15,
            SellerOfferStatus.Active,
            new DateTime(2026, 1, 3, 10, 0, 0, DateTimeKind.Utc));

        // Another seller's active offer must not affect TotalCount.
        await SeedOfferAsync(
            otherSellerId,
            "Other Active",
            "Standard",
            400m,
            20,
            SellerOfferStatus.Active,
            new DateTime(2026, 1, 4, 10, 0, 0, DateTimeKind.Utc));

        using var provider = CreateProvider(sellerId);

        // Act
        var firstPage = await SendAsync(
            provider,
            new GetMySellerOffersQuery(
                Page: 1,
                PageSize: 1,
                Status: SellerOfferStatus.Active));

        var secondPage = await SendAsync(
            provider,
            new GetMySellerOffersQuery(
                Page: 2,
                PageSize: 1,
                Status: SellerOfferStatus.Active));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(firstPage.Items, Has.Count.EqualTo(1));
            Assert.That(firstPage.Items[0].Id, Is.EqualTo(newestActiveOffer.Id));
            Assert.That(firstPage.TotalCount, Is.EqualTo(2));
            Assert.That(firstPage.Page, Is.EqualTo(1));

            Assert.That(secondPage.Items, Has.Count.EqualTo(1));
            Assert.That(secondPage.Items[0].Id, Is.EqualTo(oldestActiveOffer.Id));
            Assert.That(secondPage.TotalCount, Is.EqualTo(2));
            Assert.That(secondPage.Page, Is.EqualTo(2));
        });
    }

    private async Task<SeededOffer> SeedOfferAsync(
        Guid sellerId,
        string productName,
        string? variantName,
        decimal price,
        int stock,
        SellerOfferStatus status,
        DateTime createdAt)
    {
        var suffix = Guid.NewGuid().ToString("N");

        var brand = Brand.Create($"Brand-{suffix}");
        var category = Category.Create($"Category-{suffix}");

        var product = Product.Create(
            productName,
            $"Description for {productName}",
            brand.Id,
            category.Id);

        product.Activate();

        var sku = $"SKU-{suffix}";

        var variant = ProductVariant.Create(
            product,
            sku,
            variantName);

        variant.Activate(product);

        var offer = SellerOffer.Create(
            sellerId,
            variant.Id,
            price,
            stock);

        if (status == SellerOfferStatus.Active)
            offer.Activate();

        await using var context = CreateDbContext();

        context.AddRange(
            brand,
            category,
            product,
            variant,
            offer);

        context.Entry(offer)
            .Property(x => x.CreatedAt)
            .CurrentValue = createdAt;

        await context.SaveChangesAsync();

        return new SeededOffer(
            offer.Id,
            product.Id,
            variant.Id,
            sku);
    }

    private ServiceProvider CreateProvider(Guid? currentUserId)
    {
        var configuration = new ConfigurationManager();

        configuration["ConnectionStrings:ECommerceDb"] = ConnectionString;
        configuration["Jwt:Issuer"] = "ECommerce.Tests";
        configuration["Jwt:Audience"] = "ECommerce.Tests";
        configuration["Jwt:ExpirationMinutes"] = "60";
        configuration["Jwt:SecretKey"] = new string('t', 32);

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        services.AddScoped<ICurrentUser>(
            _ => new TestCurrentUser(currentUserId));

        return services.BuildServiceProvider();
    }

    private static async Task<GetMySellerOffersResponse> SendAsync(
        IServiceProvider provider,
        GetMySellerOffersQuery? query = null)
    {
        using var scope = provider.CreateScope();

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        return await sender.Send(
            query ?? new GetMySellerOffersQuery());
    }

    private sealed record SeededOffer(
        Guid Id,
        Guid ProductId,
        Guid ProductVariantId,
        string SKU);

    private sealed class TestCurrentUser(Guid? userId) : ICurrentUser
    {
        public Guid? UserId { get; } = userId;

        public bool IsAuthenticated => UserId.HasValue;

        public bool IsInRole(string role) => false;
    }
}
