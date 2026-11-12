using ECommerce.Application.Features.Addresses.SetDefaultAddress;

namespace ECommerce.Tests.Application.Features.Addresses.SetDefaultAddress;

public sealed class SetDefaultAddressCommandValidatorTests
{
    private readonly SetDefaultAddressCommandValidator _validator = new();

    [Test]
    public async Task Validate_should_accept_non_empty_address_id()
    {
        var result = await _validator.ValidateAsync(
            new SetDefaultAddressCommand(Guid.NewGuid()));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public async Task Validate_should_reject_empty_address_id()
    {
        var result = await _validator.ValidateAsync(
            new SetDefaultAddressCommand(Guid.Empty));

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Count.EqualTo(1));
    }
}
