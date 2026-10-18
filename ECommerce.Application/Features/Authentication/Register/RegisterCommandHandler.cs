using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using MediatR;

namespace ECommerce.Application.Features.Authentication.Register;

public sealed class RegisterCommandHandler(
    IIdentityService identityService,
    IUserRoleService userRoleService)
    : IRequestHandler<RegisterCommand, RegisterResponseDto>
{
    public async Task<RegisterResponseDto> Handle(
        RegisterCommand command,
        CancellationToken cancellationToken)
    {
        var user = await identityService.CreateUserAsync(
            command.Email.Trim(),
            command.Password,
            command.FirstName.Trim(),
            command.LastName.Trim(),
            cancellationToken);

        try
        {
            await userRoleService.AddToRoleAsync(
                user.Id,
                ApplicationRoles.Customer,
                cancellationToken);
        }
        catch (Exception roleAssignmentException)
        {
            try
            {
                // Compensate for user creation so a role-assignment failure does not leave an unusable account.
                await identityService.DeleteUserAsync(user.Id, CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                throw new InvalidOperationException(
                    "Registration failed while assigning the Customer role, and the created account could not be removed.",
                    new AggregateException(roleAssignmentException, cleanupException));
            }

            throw;
        }

        return new RegisterResponseDto(user.Id, user.Email);
    }
}
