using ECommerce.Application.Abstractions.Identity;
using ECommerce.Domain.Entities;
using ECommerce.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ECommerce.Infrastructure.Persistence.Seed;

public static class DatabaseSeeder
{
    public static async Task SeedRolesAsync(
        RoleManager<IdentityRole<Guid>> roleManager)
    {
        await EnsureRoleAsync(roleManager, ApplicationRoles.Admin);
        await EnsureRoleAsync(roleManager, ApplicationRoles.Seller);
        await EnsureRoleAsync(roleManager, ApplicationRoles.Customer);
    }

    public static async Task SeedDevelopmentDataAsync(
        ECommerceDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken = default)
    {
        await SeedCatalogDataAsync(dbContext, cancellationToken);
        await SeedUsersAsync(userManager);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task EnsureRoleAsync(
        RoleManager<IdentityRole<Guid>> roleManager,
        string role)
    {
        if (await roleManager.RoleExistsAsync(role))
            return;

        var result = await roleManager.CreateAsync(
            new IdentityRole<Guid>(role));

        if (!result.Succeeded)
        {
            var errors = string.Join(
                "; ",
                result.Errors.Select(error => error.Description));

            throw new InvalidOperationException(
                $"Failed to seed role '{role}': {errors}");
        }
    }

    private static async Task SeedCatalogDataAsync(
        ECommerceDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Brands.AnyAsync(cancellationToken))
        {
            dbContext.Brands.Add(Brand.Create("Apple"));
        }

        if (!await dbContext.Categories.AnyAsync(cancellationToken))
        {
            dbContext.Categories.Add(Category.Create("Mobile Phones"));
        }
    }

    private static async Task SeedUsersAsync(
        UserManager<ApplicationUser> userManager)
    {
        var seller = await EnsureUserAsync(
            userManager,
            email: "seller@test.com",
            password: "Password123!",
            firstName: "Test",
            lastName: "Seller");

        await EnsureRoleAsync(
            userManager,
            seller,
            ApplicationRoles.Seller);

        var admin = await EnsureUserAsync(
            userManager,
            email: "admin@test.com",
            password: "Password123!",
            firstName: "Test",
            lastName: "Admin");

        await EnsureRoleAsync(
            userManager,
            admin,
            ApplicationRoles.Admin);
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string firstName,
        string lastName)
    {
        var user = await userManager.FindByEmailAsync(email);

        if (user is not null)
            return user;

        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName
        };

        var result = await userManager.CreateAsync(user, password);

        if (!result.Succeeded)
        {
            var errors = string.Join(
                "; ",
                result.Errors.Select(error => error.Description));

            throw new InvalidOperationException(
                $"Failed to seed user '{email}': {errors}");
        }

        return user;
    }

    private static async Task EnsureRoleAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        string role)
    {
        if (await userManager.IsInRoleAsync(user, role))
            return;

        var result = await userManager.AddToRoleAsync(user, role);

        if (!result.Succeeded)
        {
            var errors = string.Join(
                "; ",
                result.Errors.Select(error => error.Description));

            throw new InvalidOperationException(
                $"Failed to assign role '{role}' to user '{user.Id}': {errors}");
        }
    }
}