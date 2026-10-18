using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using Microsoft.AspNetCore.Identity;

namespace ECommerce.Infrastructure.Identity;

internal sealed class IdentityService(UserManager<ApplicationUser> userManager) : IIdentityService
{
    public async Task<AuthenticatedUser?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(email);
        if (user is null) return null;

        var passwordValid = await userManager.CheckPasswordAsync(user, password);
        if (!passwordValid) return null;

        if (string.IsNullOrWhiteSpace(user.Email)) return null;

        var roles = await userManager.GetRolesAsync(user);

        return new AuthenticatedUser(
            user.Id,
            user.Email,
            roles.ToArray());
    }

    public async Task<CreatedIdentityUser> CreateUserAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedEmail = email.Trim();
        var existingUser = await userManager.FindByEmailAsync(normalizedEmail);

        if (existingUser is not null)
            throw new BusinessRuleException("An account with this email already exists.");

        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim()
        };

        IdentityResult result;
        try
        {
            result = await userManager.CreateAsync(user, password);
        }
        catch (PersistenceConflictException)
        {
            // The unique database index closes the race between concurrent registration requests.
            throw new BusinessRuleException("An account with this email already exists.");
        }

        if (!result.Succeeded)
        {
            if (result.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
                throw new BusinessRuleException("An account with this email already exists.");

            var errors = string.Join("; ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Failed to create user: {errors}");
        }

        return new CreatedIdentityUser(user.Id, user.Email!);
    }

    public async Task DeleteUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
            return;

        var result = await userManager.DeleteAsync(user);

        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(error => error.Description));
            throw new InvalidOperationException($"Failed to remove user '{userId}': {errors}");
        }
    }
}
