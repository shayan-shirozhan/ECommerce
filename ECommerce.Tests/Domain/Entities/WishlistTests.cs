using ECommerce.Domain.Entities;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Tests.Domain.Entities;

public sealed class WishlistTests
{
    [Test]
    public void Create_should_create_an_empty_wishlist_for_user()
    {
        var userId = Guid.NewGuid();

        var wishlist = Wishlist.Create(userId);

        Assert.Multiple(() =>
        {
            Assert.That(wishlist.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(wishlist.UserId, Is.EqualTo(userId));
            Assert.That(wishlist.Items, Is.Empty);
        });
    }

    [Test]
    public void Create_should_reject_empty_user_id()
    {
        Assert.Throws<DomainException>(() => Wishlist.Create(Guid.Empty));
    }

    [Test]
    public void AddProduct_should_reject_empty_product_id()
    {
        var wishlist = Wishlist.Create(Guid.NewGuid());

        Assert.Throws<DomainException>(() => wishlist.AddProduct(Guid.Empty));
        Assert.That(wishlist.Items, Is.Empty);
    }

    [Test]
    public void AddProduct_should_add_a_new_product()
    {
        var wishlist = Wishlist.Create(Guid.NewGuid());
        var productId = Guid.NewGuid();

        var added = wishlist.AddProduct(productId);

        Assert.Multiple(() =>
        {
            Assert.That(added, Is.True);
            Assert.That(wishlist.Items, Has.Count.EqualTo(1));
            Assert.That(wishlist.Items.Single().ProductId, Is.EqualTo(productId));
        });
    }

    [Test]
    public void AddProduct_should_not_add_a_duplicate_product()
    {
        var wishlist = Wishlist.Create(Guid.NewGuid());
        var productId = Guid.NewGuid();

        var firstAdd = wishlist.AddProduct(productId);
        var secondAdd = wishlist.AddProduct(productId);

        Assert.Multiple(() =>
        {
            Assert.That(firstAdd, Is.True);
            Assert.That(secondAdd, Is.False);
            Assert.That(wishlist.Items, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void AddProduct_should_keep_different_products_as_separate_items()
    {
        var wishlist = Wishlist.Create(Guid.NewGuid());

        wishlist.AddProduct(Guid.NewGuid());
        wishlist.AddProduct(Guid.NewGuid());

        Assert.That(wishlist.Items, Has.Count.EqualTo(2));
    }

    [Test]
    public void RemoveProduct_should_reject_empty_product_id()
    {
        var wishlist = Wishlist.Create(Guid.NewGuid());

        Assert.Throws<DomainException>(() => wishlist.RemoveProduct(Guid.Empty));
    }

    [Test]
    public void RemoveProduct_should_remove_an_existing_product()
    {
        var wishlist = Wishlist.Create(Guid.NewGuid());
        var productId = Guid.NewGuid();
        wishlist.AddProduct(productId);

        var removed = wishlist.RemoveProduct(productId);

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.True);
            Assert.That(wishlist.Items, Is.Empty);
        });
    }

    [Test]
    public void RemoveProduct_should_ignore_a_missing_product()
    {
        var wishlist = Wishlist.Create(Guid.NewGuid());
        var existingProductId = Guid.NewGuid();
        wishlist.AddProduct(existingProductId);

        var removed = wishlist.RemoveProduct(Guid.NewGuid());

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.False);
            Assert.That(wishlist.Items, Has.Count.EqualTo(1));
            Assert.That(wishlist.Items.Single().ProductId, Is.EqualTo(existingProductId));
        });
    }

    [Test]
    public void Items_should_not_allow_callers_to_mutate_the_collection()
    {
        var wishlist = Wishlist.Create(Guid.NewGuid());
        wishlist.AddProduct(Guid.NewGuid());

        Assert.That(wishlist.Items, Is.Not.InstanceOf<List<WishlistItem>>());
    }
}
