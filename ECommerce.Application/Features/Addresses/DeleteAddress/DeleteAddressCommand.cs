using MediatR;

namespace ECommerce.Application.Features.Addresses.DeleteAddress;

public sealed record DeleteAddressCommand(Guid AddressId) : IRequest;
