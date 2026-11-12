using ECommerce.Domain.Common;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Domain.Entities;

public class Address : AuditableEntity
{
    private Address()
    {
    }

    private Address(
        Guid userId,
        string title,
        string recipientName,
        string phoneNumber,
        string province,
        string city,
        string postalCode,
        string addressLine,
        bool isDefault)
    {
        if (userId == Guid.Empty)
            throw new DomainException("User ID is required.");

        UserId = userId;
        Title = RequireValue(title, nameof(title));
        RecipientName = RequireValue(recipientName, nameof(recipientName));
        PhoneNumber = RequireValue(phoneNumber, nameof(phoneNumber));
        Province = RequireValue(province, nameof(province));
        City = RequireValue(city, nameof(city));
        PostalCode = RequireValue(postalCode, nameof(postalCode));
        AddressLine = RequireValue(addressLine, nameof(addressLine));
        IsDefault = isDefault;
    }

    public Guid UserId { get; private set; }
    public string Title { get; private set; } = null!;
    public string RecipientName { get; private set; } = null!;
    public string PhoneNumber { get; private set; } = null!;
    public string Province { get; private set; } = null!;
    public string City { get; private set; } = null!;
    public string PostalCode { get; private set; } = null!;
    public string AddressLine { get; private set; } = null!;
    public bool IsDefault { get; private set; }

    public static Address Create(
        Guid userId,
        string title,
        string recipientName,
        string phoneNumber,
        string province,
        string city,
        string postalCode,
        string addressLine,
        bool isDefault = false)
    {
        return new Address(
            userId,
            title,
            recipientName,
            phoneNumber,
            province,
            city,
            postalCode,
            addressLine,
            isDefault);
    }

    public void SetAsDefault()
    {
        IsDefault = true;
    }

    public void UnsetAsDefault()
    {
        IsDefault = false;
    }

    public void UpdateDetails(
        string title,
        string recipientName,
        string phoneNumber,
        string province,
        string city,
        string postalCode,
        string addressLine)
    {
        // Validate and normalize every value before mutating the entity so a failed
        // update cannot leave the aggregate partially changed in memory.
        var normalizedTitle = RequireValue(title, nameof(title));
        var normalizedRecipientName = RequireValue(recipientName, nameof(recipientName));
        var normalizedPhoneNumber = RequireValue(phoneNumber, nameof(phoneNumber));
        var normalizedProvince = RequireValue(province, nameof(province));
        var normalizedCity = RequireValue(city, nameof(city));
        var normalizedPostalCode = RequireValue(postalCode, nameof(postalCode));
        var normalizedAddressLine = RequireValue(addressLine, nameof(addressLine));

        Title = normalizedTitle;
        RecipientName = normalizedRecipientName;
        PhoneNumber = normalizedPhoneNumber;
        Province = normalizedProvince;
        City = normalizedCity;
        PostalCode = normalizedPostalCode;
        AddressLine = normalizedAddressLine;
    }

    private static string RequireValue(string value, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{propertyName} is required.");

        return value.Trim();
    }
}
