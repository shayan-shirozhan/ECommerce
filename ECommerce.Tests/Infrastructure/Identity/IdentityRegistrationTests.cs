using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Exceptions;
using ECommerce.Infrastructure.Identity;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Infrastructure.Persistence.Seed;
using ECommerce.Tests.Infrastructure.Fixtures;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Infrastructure.Identity;

public sealed class IdentityRegistrationTests : PostgresTestBase
{
    [Test]
    public async Task CreateUser_should_persist_user_and_allow_customer_role_assignment()
    {
        using var provider = CreateIdentityServiceProvider();
        using var scope = provider.CreateScope();

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        await DatabaseSeeder.SeedRolesAsync(roleManager);

        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var roleService = scope.ServiceProvider.GetRequiredService<IUserRoleService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"customer-{Guid.NewGuid():N}@test.com";

        var created = await identityService.CreateUserAsync(
            email,
            "Password123!",
            "Test",
            "Customer",
            CancellationToken.None);

        await roleService.AddToRoleAsync(created.Id, ApplicationRoles.Customer);

        var persisted = await userManager.FindByIdAsync(created.Id.ToString());
        var roles = persisted is null
            ? Array.Empty<string>()
            : (await userManager.GetRolesAsync(persisted)).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(persisted, Is.Not.Null);
            Assert.That(persisted!.Email, Is.EqualTo(email));
            Assert.That(persisted!.UserName, Is.EqualTo(email));
            Assert.That(persisted!.FirstName, Is.EqualTo("Test"));
            Assert.That(persisted!.LastName, Is.EqualTo("Customer"));
            Assert.That(roles, Does.Contain(ApplicationRoles.Customer));
        });

        Assert.That(await userManager.CheckPasswordAsync(persisted!, "Password123!"), Is.True);
    }

    [Test]
    public async Task CreateUser_should_reject_duplicate_email_case_insensitively()
    {
        using var provider = CreateIdentityServiceProvider();
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var email = $"duplicate-{Guid.NewGuid():N}@test.com";

        await identityService.CreateUserAsync(
            email,
            "Password123!",
            "First",
            "User",
            CancellationToken.None);

        var exception = Assert.ThrowsAsync<BusinessRuleException>(async () =>
            await identityService.CreateUserAsync(
                email.ToUpperInvariant(),
                "Password123!",
                "Second",
                "User",
                CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("already exists"));
    }

    [Test]
    public async Task DeleteUser_should_remove_created_user()
    {
        using var provider = CreateIdentityServiceProvider();
        using var scope = provider.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var created = await identityService.CreateUserAsync(
            $"delete-{Guid.NewGuid():N}@test.com",
            "Password123!",
            "Test",
            "Delete",
            CancellationToken.None);

        await identityService.DeleteUserAsync(created.Id, CancellationToken.None);

        Assert.That(await userManager.FindByIdAsync(created.Id.ToString()), Is.Null);
    }

    private ServiceProvider CreateIdentityServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ECommerceDbContext>(options => options.UseNpgsql(ConnectionString));
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ECommerceDbContext>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IUserRoleService, UserRoleService>();

        return services.BuildServiceProvider();
    }
}
