using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Features.Authentication.Register;

namespace ECommerce.Tests.Application.Features.Authentication.Register;

public sealed class RegisterCommandHandlerTests
{
    [Test]
    public async Task Register_should_create_user_assign_customer_role_and_return_response()
    {
        var createdUser = new CreatedIdentityUser(Guid.NewGuid(), "customer@test.com");
        var identityService = new FakeIdentityService { CreatedUser = createdUser };
        var userRoleService = new FakeUserRoleService();
        var handler = new RegisterCommandHandler(identityService, userRoleService);

        var result = await handler.Handle(
            new RegisterCommand(
                "  customer@test.com  ",
                "Password123!",
                "  Test  ",
                "  Customer  "),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.UserId, Is.EqualTo(createdUser.Id));
            Assert.That(result.Email, Is.EqualTo(createdUser.Email));
            Assert.That(identityService.LastEmail, Is.EqualTo("customer@test.com"));
            Assert.That(identityService.LastFirstName, Is.EqualTo("Test"));
            Assert.That(identityService.LastLastName, Is.EqualTo("Customer"));
            Assert.That(userRoleService.LastUserId, Is.EqualTo(createdUser.Id));
            Assert.That(userRoleService.LastRole, Is.EqualTo(ApplicationRoles.Customer));
            Assert.That(identityService.DeleteUserCalled, Is.False);
        });
    }

    [Test]
    public void Register_should_delete_created_user_and_rethrow_when_role_assignment_fails()
    {
        var createdUser = new CreatedIdentityUser(Guid.NewGuid(), "customer@test.com");
        var identityService = new FakeIdentityService { CreatedUser = createdUser };
        var userRoleService = new FakeUserRoleService
        {
            ExceptionToThrow = new InvalidOperationException("Role assignment failed.")
        };
        var handler = new RegisterCommandHandler(identityService, userRoleService);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.Handle(
                new RegisterCommand("customer@test.com", "Password123!", "Test", "Customer"),
                CancellationToken.None));

        Assert.Multiple(() =>
        {
            Assert.That(exception!.Message, Is.EqualTo("Role assignment failed."));
            Assert.That(identityService.DeleteUserCalled, Is.True);
            Assert.That(identityService.DeletedUserId, Is.EqualTo(createdUser.Id));
        });
    }

    [Test]
    public void Register_should_surface_cleanup_failure_when_role_assignment_and_cleanup_fail()
    {
        var identityService = new FakeIdentityService
        {
            CreatedUser = new CreatedIdentityUser(Guid.NewGuid(), "customer@test.com"),
            DeleteException = new InvalidOperationException("Cleanup failed.")
        };
        var userRoleService = new FakeUserRoleService
        {
            ExceptionToThrow = new InvalidOperationException("Role assignment failed.")
        };
        var handler = new RegisterCommandHandler(identityService, userRoleService);

        var exception = Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await handler.Handle(
                new RegisterCommand("customer@test.com", "Password123!", "Test", "Customer"),
                CancellationToken.None));

        Assert.That(exception!.InnerException, Is.TypeOf<AggregateException>());
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        public CreatedIdentityUser CreatedUser { get; init; } =
            new(Guid.NewGuid(), "customer@test.com");

        public string? LastEmail { get; private set; }
        public string? LastFirstName { get; private set; }
        public string? LastLastName { get; private set; }
        public bool DeleteUserCalled { get; private set; }
        public Guid? DeletedUserId { get; private set; }
        public Exception? DeleteException { get; init; }

        public Task<AuthenticatedUser?> AuthenticateAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default)
            => Task.FromResult<AuthenticatedUser?>(null);

        public Task<CreatedIdentityUser> CreateUserAsync(
            string email,
            string password,
            string firstName,
            string lastName,
            CancellationToken cancellationToken = default)
        {
            LastEmail = email;
            LastFirstName = firstName;
            LastLastName = lastName;
            return Task.FromResult(CreatedUser);
        }

        public Task DeleteUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            DeleteUserCalled = true;
            DeletedUserId = userId;

            return DeleteException is null
                ? Task.CompletedTask
                : Task.FromException(DeleteException);
        }
    }

    private sealed class FakeUserRoleService : IUserRoleService
    {
        public Guid? LastUserId { get; private set; }
        public string? LastRole { get; private set; }
        public Exception? ExceptionToThrow { get; init; }

        public Task AddToRoleAsync(
            Guid userId,
            string role,
            CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            LastRole = role;

            return ExceptionToThrow is null
                ? Task.CompletedTask
                : Task.FromException(ExceptionToThrow);
        }
    }
}
