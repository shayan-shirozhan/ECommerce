using System.Data;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ECommerce.Infrastructure.Identity;

/// <summary>
/// Creates the first production administrator through an explicit one-time operation.
/// Credentials are read from the BootstrapAdmin configuration section, normally supplied
/// through environment variables. They must never be committed to appsettings files.
/// </summary>
public static class InitialAdminBootstrapper
{
    public static async Task CreateAsync(
        ECommerceDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(roleManager);
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection("BootstrapAdmin");
        var email = section["Email"]?.Trim();
        var password = section["Password"];
        var firstName = section["FirstName"]?.Trim();
        var lastName = section["LastName"]?.Trim();

        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("BootstrapAdmin:Email must be provided through a secure configuration source.");

        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("BootstrapAdmin:Password must be provided through a secure configuration source.");

        if (string.IsNullOrWhiteSpace(firstName))
            throw new InvalidOperationException("BootstrapAdmin:FirstName must be provided.");

        if (string.IsNullOrWhiteSpace(lastName))
            throw new InvalidOperationException("BootstrapAdmin:LastName must be provided.");

        // Serializable isolation prevents two simultaneous bootstrap invocations from both
        // observing an empty Admin role and creating separate first administrators.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        try
        {
            if (!await roleManager.RoleExistsAsync(ApplicationRoles.Admin))
            {
                var createRoleResult = await roleManager.CreateAsync(
                    new IdentityRole<Guid>(ApplicationRoles.Admin));

                EnsureSucceeded(createRoleResult, "create the Admin role");
            }

            var existingAdmins = await userManager.GetUsersInRoleAsync(ApplicationRoles.Admin);
            if (existingAdmins.Count > 0)
                throw new InvalidOperationException(
                    "An administrator already exists. Initial administrator bootstrap is a one-time operation.");

            if (await userManager.FindByEmailAsync(email) is not null)
                throw new InvalidOperationException(
                    "The configured bootstrap email already belongs to an account. No existing account was modified.");

            var admin = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = firstName,
                LastName = lastName
            };

            var createUserResult = await userManager.CreateAsync(admin, password);
            EnsureSucceeded(createUserResult, "create the initial administrator account");

            var addToRoleResult = await userManager.AddToRoleAsync(admin, ApplicationRoles.Admin);
            EnsureSucceeded(addToRoleResult, "assign the Admin role to the initial administrator");

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
            return;

        var errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"Failed to {operation}: {errors}");
    }
}
