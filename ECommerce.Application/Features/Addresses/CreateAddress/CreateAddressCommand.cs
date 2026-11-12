using MediatR;

namespace ECommerce.Application.Features.Addresses.CreateAddress;

public sealed record CreateAddressCommand(
    string Title,
    string RecipientName,
    string PhoneNumber,
    string Province,
    string City,
    string PostalCode,
    string AddressLine,
    bool IsDefault = false) : IRequest<CreateAddressResponse>;
