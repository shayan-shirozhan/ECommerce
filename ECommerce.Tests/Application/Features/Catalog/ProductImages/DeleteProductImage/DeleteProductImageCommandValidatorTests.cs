using ECommerce.Application.Features.Catalog.ProductImages.DeleteProductImage;

namespace ECommerce.Tests.Application.Features.Catalog.ProductImages.DeleteProductImage;

public sealed class DeleteProductImageCommandValidatorTests
{
    private readonly DeleteProductImageCommandValidator _validator = new();

    [Test]
    public void Validate_should_fail_when_product_image_id_is_empty()
    {
        var result = _validator.Validate(new DeleteProductImageCommand(Guid.Empty));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(
                result.Errors.Select(error => error.PropertyName),
                Does.Contain(nameof(DeleteProductImageCommand.ProductImageId)));
        });
    }

    [Test]
    public void Validate_should_succeed_when_product_image_id_is_provided()
    {
        var result = _validator.Validate(new DeleteProductImageCommand(Guid.NewGuid()));

        Assert.That(result.IsValid, Is.True);
    }
}
