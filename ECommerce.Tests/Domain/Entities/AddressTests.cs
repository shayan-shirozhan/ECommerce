using ECommerce.Domain.Entities;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Tests.Domain.Entities;

public sealed class AddressTests
{
    [Test]
    public void Create_should_trim_required_text_values()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var address = Address.Create(
            userId,
            "  Home  ",
            "  Test Recipient  ",
            "  09120000000  ",
            "  Tehran  ",
            "  Tehran  ",
            "  1234567890  ",
            "  Test Street 1  ",
            isDefault: true);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(address.UserId, Is.EqualTo(userId));
            Assert.That(address.Title, Is.EqualTo("Home"));
            Assert.That(address.RecipientName, Is.EqualTo("Test Recipient"));
            Assert.That(address.PhoneNumber, Is.EqualTo("09120000000"));
            Assert.That(address.Province, Is.EqualTo("Tehran"));
            Assert.That(address.City, Is.EqualTo("Tehran"));
            Assert.That(address.PostalCode, Is.EqualTo("1234567890"));
            Assert.That(address.AddressLine, Is.EqualTo("Test Street 1"));
            Assert.That(address.IsDefault, Is.True);
        });
    }

    [Test]
    public void Create_should_reject_empty_user_id()
    {
        Assert.Throws<DomainException>(() => Address.Create(
            Guid.Empty,
            "Home",
            "Test Recipient",
            "09120000000",
            "Tehran",
            "Tehran",
            "1234567890",
            "Test Street 1"));
    }

    [Test]
    public void Create_should_reject_blank_required_text_values()
    {
        Assert.Throws<DomainException>(() => Address.Create(
            Guid.NewGuid(),
            " ",
            "Test Recipient",
            "09120000000",
            "Tehran",
            "Tehran",
            "1234567890",
            "Test Street 1"));
    }

    [Test]
    public void SetAsDefault_should_mark_address_as_default()
    {
        var address = CreateAddress();

        address.SetAsDefault();

        Assert.That(address.IsDefault, Is.True);
    }

    [Test]
    public void UnsetAsDefault_should_clear_default_flag()
    {
        var address = CreateAddress(isDefault: true);

        address.UnsetAsDefault();

        Assert.That(address.IsDefault, Is.False);
    }

    [Test]
    public void UpdateDetails_should_trim_and_update_address_fields_without_changing_default_status()
    {
        var address = CreateAddress(isDefault: true);

        address.UpdateDetails(
            "  Office  ",
            "  Updated Recipient  ",
            "  09121111111  ",
            "  Alborz  ",
            "  Karaj  ",
            "  9876543210  ",
            "  Updated Street  ");

        Assert.Multiple(() =>
        {
            Assert.That(address.Title, Is.EqualTo("Office"));
            Assert.That(address.RecipientName, Is.EqualTo("Updated Recipient"));
            Assert.That(address.PhoneNumber, Is.EqualTo("09121111111"));
            Assert.That(address.Province, Is.EqualTo("Alborz"));
            Assert.That(address.City, Is.EqualTo("Karaj"));
            Assert.That(address.PostalCode, Is.EqualTo("9876543210"));
            Assert.That(address.AddressLine, Is.EqualTo("Updated Street"));
            Assert.That(address.IsDefault, Is.True);
        });
    }

    [Test]
    public void UpdateDetails_should_reject_blank_required_values()
    {
        var address = CreateAddress();

        Assert.Throws<DomainException>(() => address.UpdateDetails(
            "Office",
            "Updated Recipient",
            "09121111111",
            "Tehran",
            "Tehran",
            "9876543210",
            " "));

        Assert.That(address.Title, Is.EqualTo("Home"));
    }

    private static Address CreateAddress(bool isDefault = false)
    {
        return Address.Create(
            Guid.NewGuid(),
            "Home",
            "Test Recipient",
            "09120000000",
            "Tehran",
            "Tehran",
            "1234567890",
            "Test Street 1",
            isDefault);
    }
}
