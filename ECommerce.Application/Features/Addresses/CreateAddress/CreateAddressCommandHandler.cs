using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using ECommerce.Domain.Entities;
using MediatR;

namespace ECommerce.Application.Features.Addresses.CreateAddress;

public sealed class CreateAddressCommandHandler(
    ICurrentUser currentUser,
    IAddressRepository addressRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CreateAddressCommand, CreateAddressResponse>
{
    public async Task<CreateAddressResponse> Handle(
        CreateAddressCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Authenticated user is required.");

        var existingAddresses = await addressRepository.GetTrackedByUserIdAsync(
            userId,
            cancellationToken);

        // The first address is always the default. A new address also becomes default
        // when no current default exists, or when the client explicitly requests it.
        var shouldBeDefault = !existingAddresses.Any(address => address.IsDefault) || command.IsDefault;

        if (shouldBeDefault)
        {
            foreach (var existingDefaultAddress in existingAddresses.Where(address => address.IsDefault))
                existingDefaultAddress.UnsetAsDefault();
        }

        var address = Address.Create(
            userId,
            command.Title,
            command.RecipientName,
            command.PhoneNumber,
            command.Province,
            command.City,
            command.PostalCode,
            command.AddressLine,
            shouldBeDefault);

        await addressRepository.AddAsync(address, cancellationToken);

        // Existing default changes and the new address are persisted by one SaveChanges call.
        // The filtered unique index is the final guard against concurrent default creation.
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateAddressResponse(
            address.Id,
            address.IsDefault,
            address.CreatedAt);
    }
}
