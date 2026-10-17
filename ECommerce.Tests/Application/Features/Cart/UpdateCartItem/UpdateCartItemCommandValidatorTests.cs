using ECommerce.Application.Features.Cart.UpdateCartItem;

namespace ECommerce.Tests.Application.Features.Cart.UpdateCartItem;

public sealed class UpdateCartItemCommandValidatorTests
{
    private readonly UpdateCartItemCommandValidator _validator = new();

    [Test]
    public async Task Validate_should_reject_empty_seller_offer_id()
    {
        var result = await _validator.ValidateAsync(new UpdateCartItemCommand(Guid.Empty, 1));

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Any(error => error.PropertyName == nameof(UpdateCartItemCommand.SellerOfferId)),
            Is.True);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public async Task Validate_should_reject_non_positive_quantity(int quantity)
    {
        var result = await _validator.ValidateAsync(
            new UpdateCartItemCommand(Guid.NewGuid(), quantity));

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Any(error => error.PropertyName == nameof(UpdateCartItemCommand.Quantity)),
            Is.True);
    }

    [Test]
    public async Task Validate_should_accept_valid_command()
    {
        var result = await _validator.ValidateAsync(
            new UpdateCartItemCommand(Guid.NewGuid(), 2));

        Assert.That(result.IsValid, Is.True);
    }
}
