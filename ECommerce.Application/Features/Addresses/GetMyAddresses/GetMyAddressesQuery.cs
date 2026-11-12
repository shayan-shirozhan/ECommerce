using MediatR;

namespace ECommerce.Application.Features.Addresses.GetMyAddresses;

public sealed record GetMyAddressesQuery : IRequest<GetMyAddressesResponse>;
