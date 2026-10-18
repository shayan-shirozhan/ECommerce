using ECommerce.Domain.Entities;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Tests.Domain.Entities;

public sealed class CategoryTests
{
    [Test]
    public void Create_should_normalize_name_and_empty_parent_id()
    {
        var category = Category.Create("  Phones  ", Guid.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(category.Name, Is.EqualTo("Phones"));
            Assert.That(category.ParentCategoryId, Is.Null);
            Assert.That(category.IsDeleted, Is.False);
        });
    }

    [Test]
    public void UpdateDetails_should_update_name_and_parent()
    {
        var category = Category.Create("Phones");
        var parentCategoryId = Guid.NewGuid();

        category.UpdateDetails("  Smartphones  ", parentCategoryId);

        Assert.Multiple(() =>
        {
            Assert.That(category.Name, Is.EqualTo("Smartphones"));
            Assert.That(
                category.ParentCategoryId,
                Is.EqualTo(parentCategoryId));
        });
    }

    [Test]
    public void UpdateDetails_should_normalize_empty_parent_id_to_null()
    {
        var category = Category.Create("Phones", Guid.NewGuid());

        category.UpdateDetails("Phones", Guid.Empty);

        Assert.That(category.ParentCategoryId, Is.Null);
    }

    [Test]
    public void UpdateDetails_should_reject_category_as_its_own_parent()
    {
        var category = Category.Create("Phones");

        Assert.Throws<DomainException>(() =>
            category.UpdateDetails("Phones", category.Id));
    }

    [Test]
    public void UpdateDetails_should_reject_empty_name()
    {
        var category = Category.Create("Phones");

        Assert.Throws<DomainException>(() =>
            category.UpdateDetails(" ", null));
    }

    [Test]
    public void SoftDelete_should_mark_category_as_deleted()
    {
        var category = Category.Create("Phones");

        category.SoftDelete();

        Assert.That(category.IsDeleted, Is.True);
    }

    [Test]
    public void SoftDelete_should_reject_repeated_deletion()
    {
        var category = Category.Create("Phones");
        category.SoftDelete();

        Assert.Throws<DomainException>(() => category.SoftDelete());
    }

    [Test]
    public void UpdateDetails_should_reject_deleted_category()
    {
        var category = Category.Create("Phones");
        category.SoftDelete();

        Assert.Throws<DomainException>(() =>
            category.UpdateDetails("Smartphones", null));
    }
}
