namespace ECommerce.Application.Features.Addresses.GetMyAddresses;

public sealed record GetMyAddressesResponse(
    IReadOnlyList<AddressListItemResponse> Addresses);

public sealed record AddressListItemResponse(
    Guid Id,
    string Title,
    string RecipientName,
    string PhoneNumber,
    string Province,
    string City,
    string PostalCode,
    string AddressLine,
    bool IsDefault,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
