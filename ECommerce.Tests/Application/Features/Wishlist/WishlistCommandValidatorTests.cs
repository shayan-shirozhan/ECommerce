using ECommerce.Application.Features.Wishlist.AddWishlistItem;
using ECommerce.Application.Features.Wishlist.RemoveWishlistItem;

namespace ECommerce.Tests.Application.Features.Wishlist;

public sealed class WishlistCommandValidatorTests
{
    [Test]
    public void AddWishlistItem_should_reject_empty_product_id()
    {
        var validator = new AddWishlistItemCommandValidator();

        var result = validator.Validate(new AddWishlistItemCommand(Guid.Empty));

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Select(error => error.PropertyName),
            Does.Contain(nameof(AddWishlistItemCommand.ProductId)));
    }

    [Test]
    public void RemoveWishlistItem_should_reject_empty_product_id()
    {
        var validator = new RemoveWishlistItemCommandValidator();

        var result = validator.Validate(new RemoveWishlistItemCommand(Guid.Empty));

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Select(error => error.PropertyName),
            Does.Contain(nameof(RemoveWishlistItemCommand.ProductId)));
    }
}
