using MediatR;

namespace ECommerce.Application.Features.Addresses.SetDefaultAddress;

public sealed record SetDefaultAddressCommand(Guid AddressId) : IRequest;
