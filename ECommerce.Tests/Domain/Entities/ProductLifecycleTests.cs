using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Tests.Domain.Entities;

public sealed class ProductLifecycleTests
{
    [Test]
    public void Activate_should_change_draft_product_to_active()
    {
        var product = CreateProduct();

        product.Activate();

        Assert.That(product.Status, Is.EqualTo(ProductStatus.Active));
    }

    [Test]
    public void Activate_should_allow_reactivating_inactive_product()
    {
        var product = CreateProduct();
        product.Activate();
        product.Deactivate();

        product.Activate();

        Assert.That(product.Status, Is.EqualTo(ProductStatus.Active));
    }

    [Test]
    public void Activate_should_reject_already_active_product()
    {
        var product = CreateProduct();
        product.Activate();

        Assert.Throws<DomainException>(() => product.Activate());
    }

    [Test]
    public void Deactivate_should_change_active_product_to_inactive()
    {
        var product = CreateProduct();
        product.Activate();

        product.Deactivate();

        Assert.That(product.Status, Is.EqualTo(ProductStatus.Inactive));
    }

    [Test]
    public void Deactivate_should_change_draft_product_to_inactive()
    {
        var product = CreateProduct();

        product.Deactivate();

        Assert.That(product.Status, Is.EqualTo(ProductStatus.Inactive));
    }

    private static Product CreateProduct() =>
        Product.Create("Phone", "Phone description", Guid.NewGuid(), Guid.NewGuid());
}

public sealed class ProductVariantLifecycleTests
{
    [Test]
    public void Activate_should_change_variant_to_active_when_parent_product_is_active()
    {
        var product = CreateActiveProduct();
        var variant = ProductVariant.Create(product, "PHONE-BLK", "Black");

        variant.Activate(product);

        Assert.That(variant.Status, Is.EqualTo(ProductVariantStatus.Active));
    }

    [Test]
    public void Activate_should_reject_variant_when_parent_product_is_not_active()
    {
        var product = CreateProduct();
        var variant = ProductVariant.Create(product, "PHONE-BLK", "Black");

        Assert.Throws<DomainException>(() => variant.Activate(product));
        Assert.That(variant.Status, Is.EqualTo(ProductVariantStatus.Draft));
    }

    [Test]
    public void Activate_should_reject_variant_for_another_product()
    {
        var product = CreateActiveProduct();
        var anotherProduct = CreateActiveProduct();
        var variant = ProductVariant.Create(product, "PHONE-BLK", "Black");

        Assert.Throws<DomainException>(() => variant.Activate(anotherProduct));
    }

    [Test]
    public void Activate_should_reject_already_active_variant()
    {
        var product = CreateActiveProduct();
        var variant = ProductVariant.Create(product, "PHONE-BLK", "Black");
        variant.Activate(product);

        Assert.Throws<DomainException>(() => variant.Activate(product));
    }

    [Test]
    public void Deactivate_should_change_active_variant_to_inactive()
    {
        var product = CreateActiveProduct();
        var variant = ProductVariant.Create(product, "PHONE-BLK", "Black");
        variant.Activate(product);

        variant.Deactivate();

        Assert.That(variant.Status, Is.EqualTo(ProductVariantStatus.Inactive));
    }

    [Test]
    public void Deactivate_should_change_draft_variant_to_inactive()
    {
        var product = CreateActiveProduct();
        var variant = ProductVariant.Create(product, "PHONE-BLK", "Black");

        variant.Deactivate();

        Assert.That(variant.Status, Is.EqualTo(ProductVariantStatus.Inactive));
    }

    private static Product CreateProduct() =>
        Product.Create("Phone", "Phone description", Guid.NewGuid(), Guid.NewGuid());

    private static Product CreateActiveProduct()
    {
        var product = CreateProduct();
        product.Activate();
        return product;
    }
}
