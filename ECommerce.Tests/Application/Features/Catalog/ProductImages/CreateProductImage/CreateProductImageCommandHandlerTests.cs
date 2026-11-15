using ECommerce.Application;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Catalog.ProductImages.CreateProductImage;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Catalog.ProductImages.CreateProductImage;

public sealed class CreateProductImageCommandHandlerTests : PostgresTestBase
{
    [Test]
    public async Task Handle_should_create_product_level_image_when_variant_is_not_specified()
    {
        var product = await SeedProductAsync();
        using var provider = CreateProvider();

        var imageId = await SendAsync(provider, new CreateProductImageCommand(
            product.Id,
            null,
            "https://example.test/product.jpg",
            "Product image",
            0,
            true));

        await using var verificationContext = CreateDbContext();
        var image = await verificationContext.ProductImages.SingleAsync(candidate => candidate.Id == imageId);

        Assert.Multiple(() =>
        {
            Assert.That(image.ProductId, Is.EqualTo(product.Id));
            Assert.That(image.ProductVariantId, Is.Null);
            Assert.That(image.Url, Is.EqualTo("https://example.test/product.jpg"));
        });
    }

    [Test]
    public async Task Handle_should_create_variant_image_when_variant_belongs_to_product()
    {
        var product = await SeedProductAsync(includeVariant: true);
        using var provider = CreateProvider();

        var imageId = await SendAsync(provider, new CreateProductImageCommand(
            product.Id,
            product.VariantId,
            "https://example.test/variant.jpg",
            "Variant image",
            0,
            true));

        await using var verificationContext = CreateDbContext();
        var image = await verificationContext.ProductImages.SingleAsync(candidate => candidate.Id == imageId);

        Assert.Multiple(() =>
        {
            Assert.That(image.ProductId, Is.EqualTo(product.Id));
            Assert.That(image.ProductVariantId, Is.EqualTo(product.VariantId));
            Assert.That(image.Url, Is.EqualTo("https://example.test/variant.jpg"));
        });
    }

    [Test]
    public async Task Handle_should_reject_variant_that_belongs_to_another_product()
    {
        var targetProduct = await SeedProductAsync();
        var variantOwner = await SeedProductAsync(includeVariant: true);
        using var provider = CreateProvider();

        var exception = Assert.ThrowsAsync<BusinessRuleException>(async () =>
            await SendAsync(provider, new CreateProductImageCommand(
                targetProduct.Id,
                variantOwner.VariantId,
                "https://example.test/invalid.jpg",
                null,
                0,
                false)));

        await using var verificationContext = CreateDbContext();
        var imageWasPersisted = await verificationContext.ProductImages.AnyAsync(
            candidate => candidate.ProductId == targetProduct.Id);

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Does.Contain("must belong"));
            Assert.That(imageWasPersisted, Is.False);
        });
    }

    [Test]
    public async Task Handle_should_throw_not_found_when_product_variant_does_not_exist()
    {
        var product = await SeedProductAsync();
        using var provider = CreateProvider();

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await SendAsync(provider, new CreateProductImageCommand(
                product.Id,
                Guid.NewGuid(),
                "https://example.test/missing-variant.jpg",
                null,
                0,
                false)));

        await using var verificationContext = CreateDbContext();
        Assert.That(
            await verificationContext.ProductImages.AnyAsync(image => image.ProductId == product.Id),
            Is.False);
    }

    private async Task<SeededProduct> SeedProductAsync(bool includeVariant = false)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var brand = Brand.Create($"Brand-{suffix}");
        var category = Category.Create($"Category-{suffix}");
        var product = Product.Create(
            $"Product-{suffix}",
            "Product image ownership test",
            brand.Id,
            category.Id);

        ProductVariant? variant = null;
        if (includeVariant)
            variant = ProductVariant.Create(product.Id, $"SKU-{suffix}", "Test variant");

        await using var context = CreateDbContext();
        context.AddRange(brand, category, product);
        if (variant is not null)
            context.ProductVariants.Add(variant);
        await context.SaveChangesAsync();

        return new SeededProduct(product.Id, variant?.Id);
    }

    private ServiceProvider CreateProvider()
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

        return services.BuildServiceProvider();
    }

    private static async Task<Guid> SendAsync(
        IServiceProvider provider,
        CreateProductImageCommand command)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(command);
    }

    private sealed record SeededProduct(Guid Id, Guid? VariantId);
}
