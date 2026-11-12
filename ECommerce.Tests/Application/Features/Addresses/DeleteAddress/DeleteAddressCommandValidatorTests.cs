using ECommerce.Application.Features.Addresses.DeleteAddress;

namespace ECommerce.Tests.Application.Features.Addresses.DeleteAddress;

public sealed class DeleteAddressCommandValidatorTests
{
    private readonly DeleteAddressCommandValidator _validator = new();

    [Test]
    public async Task Validate_should_accept_non_empty_address_id()
    {
        var result = await _validator.ValidateAsync(
            new DeleteAddressCommand(Guid.NewGuid()));

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public async Task Validate_should_reject_empty_address_id()
    {
        var result = await _validator.ValidateAsync(
            new DeleteAddressCommand(Guid.Empty));

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Count.EqualTo(1));
    }
}
