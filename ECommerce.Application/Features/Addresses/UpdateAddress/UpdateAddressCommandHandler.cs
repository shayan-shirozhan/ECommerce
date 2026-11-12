using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Addresses.UpdateAddress;

public sealed class UpdateAddressCommandHandler(
    ICurrentUser currentUser,
    IAddressRepository addressRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateAddressCommand>
{
    public async Task Handle(
        UpdateAddressCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Authenticated user is required.");

        var address = await addressRepository.GetByIdForUserAsync(
            command.AddressId,
            userId,
            cancellationToken);

        if (address is null)
            throw new NotFoundException(
                $"Address with id '{command.AddressId}' was not found.");

        address.UpdateDetails(
            command.Title,
            command.RecipientName,
            command.PhoneNumber,
            command.Province,
            command.City,
            command.PostalCode,
            command.AddressLine);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
