using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Abstractions.Persistence;
using MediatR;

namespace ECommerce.Application.Features.Authentication.ResetPassword;

public sealed class ResetPasswordCommandHandler(
    IIdentityService identityService,
    IRefreshTokenService refreshTokenService,
    IUnitOfWork unitOfWork) : IRequestHandler<ResetPasswordCommand>
{
    public async Task Handle(
        ResetPasswordCommand command,
        CancellationToken cancellationToken)
    {
        // Changing the password and revoking every refresh-token session must be atomic.
        await unitOfWork.ExecuteInTransactionAsync(async transactionCancellationToken =>
        {
            var userId = await identityService.ResetPasswordAsync(
                command.Email.Trim(),
                command.Token,
                command.NewPassword,
                transactionCancellationToken);

            await refreshTokenService.RevokeAllForUserAsync(
                userId,
                transactionCancellationToken);
        }, cancellationToken);
    }
}
