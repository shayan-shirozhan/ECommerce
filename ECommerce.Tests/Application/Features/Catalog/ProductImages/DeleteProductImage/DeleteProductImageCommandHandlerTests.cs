using ECommerce.Application;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Catalog.ProductImages.DeleteProductImage;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Catalog.ProductImages.DeleteProductImage;

public sealed class DeleteProductImageCommandHandlerTests : PostgresTestBase
{
    [Test]
    public async Task Handle_should_delete_product_image_and_preserve_product_and_other_images()
    {
        // Arrange
        var seeded = await SeedProductWithImagesAsync();
        using var provider = CreateProvider();

        // Act
        await SendAsync(provider, new DeleteProductImageCommand(seeded.ImageToDeleteId));

        // Assert
        await using var verificationContext = CreateDbContext();
        var productExists = await verificationContext.Products
            .AnyAsync(product => product.Id == seeded.ProductId);
        var remainingImageIds = await verificationContext.ProductImages
            .Where(image => image.ProductId == seeded.ProductId)
            .Select(image => image.Id)
            .ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(productExists, Is.True);
            Assert.That(remainingImageIds, Does.Not.Contain(seeded.ImageToDeleteId));
            Assert.That(remainingImageIds, Is.EquivalentTo(new[] { seeded.ImageToKeepId }));
        });
    }

    [Test]
    public void Handle_should_throw_not_found_when_product_image_does_not_exist()
    {
        // Arrange
        using var provider = CreateProvider();

        // Act + Assert
        Assert.ThrowsAsync<NotFoundException>(async () =>
            await SendAsync(provider, new DeleteProductImageCommand(Guid.NewGuid())));
    }

    private async Task<SeededProductImages> SeedProductWithImagesAsync()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var brand = Brand.Create($"Brand-{suffix}");
        var category = Category.Create($"Category-{suffix}");
        var product = Product.Create(
            $"Product-{suffix}",
            "Product image deletion test",
            brand.Id,
            category.Id);

        var imageToDelete = ProductImage.Create(
            product.Id,
            $"https://example.test/{suffix}/delete.jpg",
            "Image to delete",
            sortOrder: 0,
            isPrimary: false);
        var imageToKeep = ProductImage.Create(
            product.Id,
            $"https://example.test/{suffix}/keep.jpg",
            "Image to keep",
            sortOrder: 1,
            isPrimary: true);

        await using var context = CreateDbContext();
        context.AddRange(brand, category, product);
        context.ProductImages.AddRange(imageToDelete, imageToKeep);
        await context.SaveChangesAsync();

        return new SeededProductImages(product.Id, imageToDelete.Id, imageToKeep.Id);
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

    private static async Task SendAsync(
        IServiceProvider provider,
        DeleteProductImageCommand command)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        await sender.Send(command);
    }

    private sealed record SeededProductImages(
        Guid ProductId,
        Guid ImageToDeleteId,
        Guid ImageToKeepId);
}
