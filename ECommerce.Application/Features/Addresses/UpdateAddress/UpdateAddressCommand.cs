using MediatR;

namespace ECommerce.Application.Features.Addresses.UpdateAddress;

public sealed record UpdateAddressCommand(
    Guid AddressId,
    string Title,
    string RecipientName,
    string PhoneNumber,
    string Province,
    string City,
    string PostalCode,
    string AddressLine) : IRequest;
