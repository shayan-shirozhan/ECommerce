using ECommerce.Domain.Entities;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Tests.Domain.Entities;

public sealed class ProductDetailsTests
{
    [Test]
    public void UpdateDetails_should_update_and_trim_product_details()
    {
        var product = CreateProduct();
        var brandId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        product.UpdateDetails(
            "  Updated phone  ",
            "  Updated description  ",
            brandId,
            categoryId);

        Assert.Multiple(() =>
        {
            Assert.That(product.Name, Is.EqualTo("Updated phone"));
            Assert.That(product.Description, Is.EqualTo("Updated description"));
            Assert.That(product.BrandId, Is.EqualTo(brandId));
            Assert.That(product.CategoryId, Is.EqualTo(categoryId));
        });
    }

    [TestCase("")]
    [TestCase(" ")]
    public void UpdateDetails_should_reject_empty_name_without_mutating_product(string name)
    {
        var product = CreateProduct();
        var originalBrandId = product.BrandId;
        var originalCategoryId = product.CategoryId;

        Assert.Throws<DomainException>(() => product.UpdateDetails(
            name,
            "Updated description",
            Guid.NewGuid(),
            Guid.NewGuid()));

        Assert.Multiple(() =>
        {
            Assert.That(product.Name, Is.EqualTo("Original phone"));
            Assert.That(product.Description, Is.EqualTo("Original description"));
            Assert.That(product.BrandId, Is.EqualTo(originalBrandId));
            Assert.That(product.CategoryId, Is.EqualTo(originalCategoryId));
        });
    }

    [Test]
    public void UpdateDetails_should_reject_empty_description()
    {
        var product = CreateProduct();

        Assert.Throws<DomainException>(() => product.UpdateDetails(
            "Updated phone",
            " ",
            Guid.NewGuid(),
            Guid.NewGuid()));

        Assert.That(product.Description, Is.EqualTo("Original description"));
    }

    [Test]
    public void UpdateDetails_should_reject_empty_brand_id()
    {
        var product = CreateProduct();

        Assert.Throws<DomainException>(() => product.UpdateDetails(
            "Updated phone",
            "Updated description",
            Guid.Empty,
            Guid.NewGuid()));

        Assert.That(product.Name, Is.EqualTo("Original phone"));
    }

    [Test]
    public void UpdateDetails_should_reject_empty_category_id()
    {
        var product = CreateProduct();

        Assert.Throws<DomainException>(() => product.UpdateDetails(
            "Updated phone",
            "Updated description",
            Guid.NewGuid(),
            Guid.Empty));

        Assert.That(product.Name, Is.EqualTo("Original phone"));
    }

    private static Product CreateProduct() =>
        Product.Create(
            "Original phone",
            "Original description",
            Guid.NewGuid(),
            Guid.NewGuid());
}
