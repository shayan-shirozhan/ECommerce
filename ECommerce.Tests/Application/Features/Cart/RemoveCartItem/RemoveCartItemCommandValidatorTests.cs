using ECommerce.Application.Features.Cart.RemoveCartItem;

namespace ECommerce.Tests.Application.Features.Cart.RemoveCartItem;

public sealed class RemoveCartItemCommandValidatorTests
{
    private readonly RemoveCartItemCommandValidator _validator = new();

    [Test]
    public async Task Validate_should_reject_empty_seller_offer_id()
    {
        var result = await _validator.ValidateAsync(new RemoveCartItemCommand(Guid.Empty));

        Assert.That(result.IsValid, Is.False);
        Assert.That(
            result.Errors.Any(error => error.PropertyName == nameof(RemoveCartItemCommand.SellerOfferId)),
            Is.True);
    }

    [Test]
    public async Task Validate_should_accept_valid_command()
    {
        var result = await _validator.ValidateAsync(new RemoveCartItemCommand(Guid.NewGuid()));

        Assert.That(result.IsValid, Is.True);
    }
}
