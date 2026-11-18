using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Users.ChangeMyPassword;

public sealed class ChangeMyPasswordCommandHandler(
    ICurrentUser currentUser,
    IIdentityService identityService,
    IRefreshTokenService refreshTokenService,
    IUnitOfWork unitOfWork) : IRequestHandler<ChangeMyPasswordCommand>
{
    public async Task Handle(
        ChangeMyPasswordCommand command,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not Guid userId)
            throw new UnauthorizedException("User is not authenticated.");

        // Keep the password update and invalidation of every refresh-token session atomic.
        await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            await identityService.ChangePasswordAsync(
                userId,
                command.CurrentPassword,
                command.NewPassword,
                transactionCancellationToken);

            await refreshTokenService.RevokeAllForUserAsync(
                userId,
                transactionCancellationToken);
        }, cancellationToken);
    }
}
