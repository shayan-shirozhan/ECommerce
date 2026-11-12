using ECommerce.Application.Features.Addresses.UpdateAddress;

namespace ECommerce.Tests.Application.Features.Addresses.UpdateAddress;

public sealed class UpdateAddressCommandValidatorTests
{
    private readonly UpdateAddressCommandValidator _validator = new();

    [Test]
    public async Task Validate_should_accept_valid_address_details()
    {
        var result = await _validator.ValidateAsync(CreateCommand());

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public async Task Validate_should_reject_empty_address_id()
    {
        var command = CreateCommand() with { AddressId = Guid.Empty };

        var result = await _validator.ValidateAsync(command);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Validate_should_reject_blank_required_fields()
    {
        var command = CreateCommand() with
        {
            Title = " ",
            RecipientName = " ",
            PhoneNumber = " ",
            Province = " ",
            City = " ",
            PostalCode = " ",
            AddressLine = " "
        };

        var result = await _validator.ValidateAsync(command);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Count.EqualTo(7));
    }

    [Test]
    public async Task Validate_should_reject_values_exceeding_database_column_limits()
    {
        var command = CreateCommand() with
        {
            Title = new string('t', 101),
            RecipientName = new string('r', 151),
            PhoneNumber = new string('1', 31),
            Province = new string('p', 101),
            City = new string('c', 101),
            PostalCode = new string('1', 31),
            AddressLine = new string('a', 501)
        };

        var result = await _validator.ValidateAsync(command);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Count.EqualTo(7));
    }

    private static UpdateAddressCommand CreateCommand()
    {
        return new UpdateAddressCommand(
            Guid.NewGuid(),
            "Home",
            "Test Recipient",
            "09120000000",
            "Tehran",
            "Tehran",
            "1234567890",
            "Test Street 1");
    }
}
