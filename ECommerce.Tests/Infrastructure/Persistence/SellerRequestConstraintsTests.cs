using ECommerce.Application.Exceptions;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ECommerce.Tests.Infrastructure.Persistence;

public sealed class SellerRequestConstraintsTests : PostgresTestBase
{
    [Test]
    public async Task Database_should_prevent_multiple_pending_requests_for_same_user()
    {
        var userId = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            var user = new ApplicationUser
            {
                Id = userId,
                UserName = $"seller-request-test-{Guid.NewGuid():N}",
                Email = $"seller-request-test-{Guid.NewGuid():N}@test.local",
                EmailConfirmed = true,
                FirstName = "Seller",
                LastName = "Request",
                SellerStatus = SellerStatus.None
            };

            var firstRequest = SellerRequest.Create(
                userId,
                "First request.");

            context.Users.Add(user);
            context.SellerRequests.Add(firstRequest);

            await context.SaveChangesAsync();
        }

        await using (var context = CreateDbContext())
        {
            var secondRequest = SellerRequest.Create(
                userId,
                "Second request.");

            context.SellerRequests.Add(secondRequest);

            var exception = Assert.ThrowsAsync<PersistenceConflictException>(
                async () => await context.SaveChangesAsync());

            var dbUpdateException =
                exception!.InnerException as DbUpdateException;

            Assert.That(
                dbUpdateException,
                Is.Not.Null);

            var postgresException =
                dbUpdateException!.InnerException as PostgresException;

            Assert.That(
                postgresException,
                Is.Not.Null);

            Assert.Multiple(() =>
            {
                Assert.That(
                    postgresException!.SqlState,
                    Is.EqualTo(PostgresErrorCodes.UniqueViolation));

                Assert.That(
                    postgresException.ConstraintName,
                    Is.EqualTo("IX_SellerRequests_UserId"));
            });
        }
    }
}
