using ECommerce.Application.Features.Cart.AddCartItem;

namespace ECommerce.Tests.Application.Features.Cart.AddCartItem;

public sealed class AddCartItemCommandValidatorTests
{
    private readonly AddCartItemCommandValidator _validator = new();

    [Test]
    public async Task Validate_should_reject_empty_seller_offer_id()
    {
        var result = await _validator.ValidateAsync(new AddCartItemCommand(Guid.Empty, 1));

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(error => error.PropertyName == nameof(AddCartItemCommand.SellerOfferId)), Is.True);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public async Task Validate_should_reject_non_positive_quantity(int quantity)
    {
        var result = await _validator.ValidateAsync(new AddCartItemCommand(Guid.NewGuid(), quantity));

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(error => error.PropertyName == nameof(AddCartItemCommand.Quantity)), Is.True);
    }

    [Test]
    public async Task Validate_should_accept_valid_command()
    {
        var result = await _validator.ValidateAsync(new AddCartItemCommand(Guid.NewGuid(), 2));

        Assert.That(result.IsValid, Is.True);
    }
}
