using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Users.UpdateMyProfile;

public sealed class UpdateMyProfileCommandHandler(
    ICurrentUser currentUser,
    IUserRepository userRepository,
    IUnitOfWork unitOfWork)
    : IRequestHandler<UpdateMyProfileCommand>
{
    public async Task Handle(
        UpdateMyProfileCommand command,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedException("Authenticated user is required.");

        await userRepository.UpdateProfileAsync(
            userId,
            command.FirstName.Trim(),
            command.LastName.Trim(),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
