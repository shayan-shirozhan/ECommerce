using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Addresses.GetMyAddresses;

public sealed class GetMyAddressesQueryHandler(
    ICurrentUser currentUser,
    IAddressRepository addressRepository)
    : IRequestHandler<GetMyAddressesQuery, GetMyAddressesResponse>
{
    public async Task<GetMyAddressesResponse> Handle(
        GetMyAddressesQuery query,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Authenticated user is required.");

        var addresses = await addressRepository.GetByUserIdAsync(
            userId,
            cancellationToken);

        var items = addresses
            .Select(address => new AddressListItemResponse(
                address.Id,
                address.Title,
                address.RecipientName,
                address.PhoneNumber,
                address.Province,
                address.City,
                address.PostalCode,
                address.AddressLine,
                address.IsDefault,
                address.CreatedAt,
                address.UpdatedAt))
            .ToList();

        return new GetMyAddressesResponse(items);
    }
}
