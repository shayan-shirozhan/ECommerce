using ECommerce.Application.Features.Catalog.ProductImages.CreateProductImage;

namespace ECommerce.Tests.Application.Features.Catalog.ProductImages.CreateProductImage;

public sealed class CreateProductImageCommandValidatorTests
{
    private readonly CreateProductImageCommandValidator _validator = new();

    [Test]
    public void Validate_should_reject_empty_product_variant_id_when_provided()
    {
        var command = new CreateProductImageCommand(
            Guid.NewGuid(),
            Guid.Empty,
            "https://example.test/image.jpg",
            null,
            0,
            false);

        var result = _validator.Validate(command);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.Errors.Select(error => error.PropertyName),
                Does.Contain(nameof(CreateProductImageCommand.ProductVariantId)));
        });
    }

    [Test]
    public void Validate_should_allow_null_product_variant_id_for_product_level_image()
    {
        var command = new CreateProductImageCommand(
            Guid.NewGuid(),
            null,
            "https://example.test/image.jpg",
            null,
            0,
            true);

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.True);
    }
}
