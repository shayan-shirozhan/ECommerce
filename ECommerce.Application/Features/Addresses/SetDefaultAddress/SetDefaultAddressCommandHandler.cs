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

        // Persist removal of the current default before setting the new one. PostgreSQL
        // enforces the filtered unique index per statement, so saving both changes in an
        // arbitrary EF Core update order can violate the index even though the final state
        // would contain only one default address. Both saves run in one transaction so a
        // failure cannot leave the user without a default address.
        await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            foreach (var currentDefaultAddress in addresses.Where(address => address.IsDefault))
                currentDefaultAddress.UnsetAsDefault();

            await unitOfWork.SaveChangesAsync(transactionCancellationToken);

            addressToSetAsDefault.SetAsDefault();
            await unitOfWork.SaveChangesAsync(transactionCancellationToken);
        }, cancellationToken);
    }
}
