namespace ECommerce.API.Contracts.Addresses;

public sealed record UpdateAddressRequest(
    string Title,
    string RecipientName,
    string PhoneNumber,
    string Province,
    string City,
    string PostalCode,
    string AddressLine);
