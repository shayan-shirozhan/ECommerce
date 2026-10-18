namespace ECommerce.Application.Abstractions.Identity;

public interface IIdentityService
{
    Task<AuthenticatedUser?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<CreatedIdentityUser> CreateUserAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default);

    Task DeleteUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
