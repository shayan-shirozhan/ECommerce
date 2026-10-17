using ECommerce.Domain.Entities;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Tests.Domain.Entities;

public sealed class CartTests
{
    [Test]
    public void Create_should_create_empty_cart_for_user()
    {
        var userId = Guid.NewGuid();

        var cart = Cart.Create(userId);

        Assert.Multiple(() =>
        {
            Assert.That(cart.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(cart.UserId, Is.EqualTo(userId));
            Assert.That(cart.Items, Is.Empty);
        });
    }

    [Test]
    public void Create_should_reject_empty_user_id()
    {
        Assert.Throws<DomainException>(() => Cart.Create(Guid.Empty));
    }

    [Test]
    public void AddItem_should_reject_empty_seller_offer_id()
    {
        var cart = Cart.Create(Guid.NewGuid());

        Assert.Throws<DomainException>(() => cart.AddItem(Guid.Empty, 1));
        Assert.That(cart.Items, Is.Empty);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void AddItem_should_reject_non_positive_quantity(int quantity)
    {
        var cart = Cart.Create(Guid.NewGuid());

        Assert.Throws<DomainException>(() => cart.AddItem(Guid.NewGuid(), quantity));
        Assert.That(cart.Items, Is.Empty);
    }

    [Test]
    public void AddItem_should_add_new_item()
    {
        var cart = Cart.Create(Guid.NewGuid());
        var sellerOfferId = Guid.NewGuid();

        cart.AddItem(sellerOfferId, 3);

        var item = cart.Items.Single();
        Assert.Multiple(() =>
        {
            Assert.That(item.SellerOfferId, Is.EqualTo(sellerOfferId));
            Assert.That(item.Quantity, Is.EqualTo(3));
        });
    }

    [Test]
    public void AddItem_should_increase_quantity_for_existing_seller_offer()
    {
        var cart = Cart.Create(Guid.NewGuid());
        var sellerOfferId = Guid.NewGuid();

        cart.AddItem(sellerOfferId, 2);
        cart.AddItem(sellerOfferId, 4);

        Assert.That(cart.Items, Has.Count.EqualTo(1));
        Assert.That(cart.Items.Single().Quantity, Is.EqualTo(6));
    }

    [Test]
    public void AddItem_should_keep_different_seller_offers_as_separate_items()
    {
        var cart = Cart.Create(Guid.NewGuid());

        cart.AddItem(Guid.NewGuid(), 2);
        cart.AddItem(Guid.NewGuid(), 5);

        Assert.That(cart.Items, Has.Count.EqualTo(2));
        Assert.That(cart.Items.Sum(item => item.Quantity), Is.EqualTo(7));
    }

    [Test]
    public void AddItem_should_reject_quantity_overflow_without_mutating_cart()
    {
        var cart = Cart.Create(Guid.NewGuid());
        var sellerOfferId = Guid.NewGuid();
        cart.AddItem(sellerOfferId, int.MaxValue);

        Assert.Throws<DomainException>(() => cart.AddItem(sellerOfferId, 1));

        Assert.That(cart.Items.Single().Quantity, Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void UpdateItem_should_change_quantity()
    {
        var cart = Cart.Create(Guid.NewGuid());
        var sellerOfferId = Guid.NewGuid();
        cart.AddItem(sellerOfferId, 2);

        cart.UpdateItem(sellerOfferId, 7);

        Assert.That(cart.Items.Single().Quantity, Is.EqualTo(7));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void UpdateItem_should_reject_non_positive_quantity(int quantity)
    {
        var cart = Cart.Create(Guid.NewGuid());
        var sellerOfferId = Guid.NewGuid();
        cart.AddItem(sellerOfferId, 2);

        Assert.Throws<DomainException>(() => cart.UpdateItem(sellerOfferId, quantity));
        Assert.That(cart.Items.Single().Quantity, Is.EqualTo(2));
    }

    [Test]
    public void UpdateItem_should_reject_missing_item()
    {
        var cart = Cart.Create(Guid.NewGuid());

        Assert.Throws<DomainException>(() => cart.UpdateItem(Guid.NewGuid(), 2));
    }

    [Test]
    public void RemoveItem_should_remove_existing_item()
    {
        var cart = Cart.Create(Guid.NewGuid());
        var sellerOfferId = Guid.NewGuid();
        cart.AddItem(sellerOfferId, 2);

        cart.RemoveItem(sellerOfferId);

        Assert.That(cart.Items, Is.Empty);
    }

    [Test]
    public void RemoveItem_should_ignore_missing_item()
    {
        var cart = Cart.Create(Guid.NewGuid());
        cart.AddItem(Guid.NewGuid(), 2);

        Assert.DoesNotThrow(() => cart.RemoveItem(Guid.NewGuid()));
        Assert.That(cart.Items, Has.Count.EqualTo(1));
    }
}
