using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Addresses.DeleteAddress;

public sealed class DeleteAddressCommandHandler(
    ICurrentUser currentUser,
    IAddressRepository addressRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<DeleteAddressCommand>
{
    public async Task Handle(
        DeleteAddressCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Authenticated user is required.");

        var addresses = await addressRepository.GetTrackedByUserIdAsync(
            userId,
            cancellationToken);

        var addressToDelete = addresses
            .SingleOrDefault(address => address.Id == command.AddressId);

        if (addressToDelete is null)
            throw new NotFoundException(
                $"Address with id '{command.AddressId}' was not found.");

        var replacementDefaultAddress = addressToDelete.IsDefault
            ? addresses
                .Where(address => address.Id != addressToDelete.Id)
                .OrderByDescending(address => address.CreatedAt)
                .ThenByDescending(address => address.Id)
                .FirstOrDefault()
            : null;

        if (replacementDefaultAddress is null)
        {
            addressRepository.Remove(addressToDelete);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        // Delete the current default before promoting its replacement. PostgreSQL checks
        // the filtered unique index per statement, so the two saves must be ordered. The
        // surrounding transaction keeps the delete and promotion atomic.
        await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            addressRepository.Remove(addressToDelete);
            await unitOfWork.SaveChangesAsync(transactionCancellationToken);

            replacementDefaultAddress.SetAsDefault();
            await unitOfWork.SaveChangesAsync(transactionCancellationToken);
        }, cancellationToken);
    }
}
