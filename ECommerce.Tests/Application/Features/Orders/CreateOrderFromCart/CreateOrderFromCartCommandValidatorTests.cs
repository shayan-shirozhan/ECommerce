using ECommerce.Application.Features.Orders.CreateOrderFromCart;

namespace ECommerce.Tests.Application.Features.Orders.CreateOrderFromCart;

public sealed class CreateOrderFromCartCommandValidatorTests
{
    private readonly CreateOrderFromCartCommandValidator _validator = new();

    [Test]
    public void Validate_should_accept_complete_shipping_details()
    {
        var command = new CreateOrderFromCartCommand(
            "Buyer Test",
            "09120000000",
            "Tehran",
            "Tehran",
            "1234567890",
            "Test Street 1");

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_should_reject_missing_or_oversized_shipping_details()
    {
        var command = new CreateOrderFromCartCommand(
            " ",
            "",
            new string('p', 101),
            "Tehran",
            "1234567890",
            new string('a', 501));

        var result = _validator.Validate(command);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Count.GreaterThanOrEqualTo(4));
    }
}
