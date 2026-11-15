using ECommerce.Domain.Entities;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Tests.Domain.Entities;

public sealed class ProductVariantDetailsTests
{
    [Test]
    public void UpdateDetails_should_update_sku_and_name()
    {
        var variant = ProductVariant.Create(Guid.NewGuid(), "SKU-OLD", "Old name");

        variant.UpdateDetails("SKU-NEW", "New name");

        Assert.Multiple(() =>
        {
            Assert.That(variant.SKU, Is.EqualTo("SKU-NEW"));
            Assert.That(variant.Name, Is.EqualTo("New name"));
        });
    }

    [Test]
    public void UpdateDetails_should_allow_null_name()
    {
        var variant = ProductVariant.Create(Guid.NewGuid(), "SKU-OLD", "Old name");

        variant.UpdateDetails("SKU-NEW", null);

        Assert.That(variant.Name, Is.Null);
    }

    [TestCase("")]
    [TestCase(" ")]
    public void UpdateDetails_should_reject_empty_sku_without_mutating_variant(string sku)
    {
        var variant = ProductVariant.Create(Guid.NewGuid(), "SKU-OLD", "Old name");

        Assert.Throws<DomainException>(() => variant.UpdateDetails(sku, "New name"));

        Assert.Multiple(() =>
        {
            Assert.That(variant.SKU, Is.EqualTo("SKU-OLD"));
            Assert.That(variant.Name, Is.EqualTo("Old name"));
        });
    }

    [Test]
    public void UpdateDetails_should_reject_sku_longer_than_100_characters()
    {
        var variant = ProductVariant.Create(Guid.NewGuid(), "SKU-OLD", "Old name");

        Assert.Throws<DomainException>(() =>
            variant.UpdateDetails(new string('a', 101), "New name"));

        Assert.That(variant.SKU, Is.EqualTo("SKU-OLD"));
    }

    [TestCase("")]
    [TestCase(" ")]
    public void UpdateDetails_should_reject_empty_name_without_mutating_variant(string name)
    {
        var variant = ProductVariant.Create(Guid.NewGuid(), "SKU-OLD", "Old name");

        Assert.Throws<DomainException>(() => variant.UpdateDetails("SKU-NEW", name));

        Assert.Multiple(() =>
        {
            Assert.That(variant.SKU, Is.EqualTo("SKU-OLD"));
            Assert.That(variant.Name, Is.EqualTo("Old name"));
        });
    }

    [Test]
    public void UpdateDetails_should_reject_name_longer_than_150_characters()
    {
        var variant = ProductVariant.Create(Guid.NewGuid(), "SKU-OLD", "Old name");

        Assert.Throws<DomainException>(() =>
            variant.UpdateDetails("SKU-NEW", new string('a', 151)));

        Assert.That(variant.SKU, Is.EqualTo("SKU-OLD"));
        Assert.That(variant.Name, Is.EqualTo("Old name"));
    }
}
