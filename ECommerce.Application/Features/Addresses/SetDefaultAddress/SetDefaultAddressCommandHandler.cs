using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Addresses.SetDefaultAddress;

public sealed class SetDefaultAddressCommandHandler(
    ICurrentUser currentUser,
    IAddressRepository addressRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<SetDefaultAddressCommand>
{
    public async Task Handle(
        SetDefaultAddressCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Authenticated user is required.");

        var addresses = await addressRepository.GetTrackedByUserIdAsync(
            userId,
            cancellationToken);

        var addressToSetAsDefault = addresses
            .SingleOrDefault(address => address.Id == command.AddressId);

        if (addressToSetAsDefault is null)
            throw new NotFoundException(
                $"Address with id '{command.AddressId}' was not found.");

        // Setting the current default again is an idempotent no-op.
        if (addressToSetAsDefault.IsDefault)
            return;

        foreach (var currentDefaultAddress in addresses.Where(address => address.IsDefault))
            currentDefaultAddress.UnsetAsDefault();

        addressToSetAsDefault.SetAsDefault();

        // The existing filtered unique index guarantees that only one default address
        // can be persisted per user, including when concurrent requests race.
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
