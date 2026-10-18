using ECommerce.Domain.Entities;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Tests.Domain.Entities;

public sealed class BrandTests
{
    [Test]
    public void Create_should_trim_brand_name()
    {
        var brand = Brand.Create("  Samsung  ");

        Assert.That(brand.Name, Is.EqualTo("Samsung"));
        Assert.That(brand.IsDeleted, Is.False);
    }

    [Test]
    public void UpdateName_should_update_name()
    {
        var brand = Brand.Create("Samsung");

        brand.UpdateName("  Apple  ");

        Assert.That(brand.Name, Is.EqualTo("Apple"));
    }

    [Test]
    public void UpdateName_should_reject_empty_name()
    {
        var brand = Brand.Create("Samsung");

        Assert.Throws<DomainException>(() => brand.UpdateName(" "));
    }

    [Test]
    public void SoftDelete_should_mark_brand_as_deleted()
    {
        var brand = Brand.Create("Samsung");

        brand.SoftDelete();

        Assert.That(brand.IsDeleted, Is.True);
    }

    [Test]
    public void SoftDelete_should_reject_repeated_deletion()
    {
        var brand = Brand.Create("Samsung");
        brand.SoftDelete();

        Assert.Throws<DomainException>(() => brand.SoftDelete());
    }

    [Test]
    public void UpdateName_should_reject_deleted_brand()
    {
        var brand = Brand.Create("Samsung");
        brand.SoftDelete();

        Assert.Throws<DomainException>(() => brand.UpdateName("Apple"));
    }
}
