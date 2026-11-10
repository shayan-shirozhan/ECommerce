using ECommerce.Application;
using ECommerce.Application.Features.Sellers.SellerRequests.GetSellerRequests;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Sellers.SellerRequests.GetSellerRequests;

public sealed class GetSellerRequestsQueryHandlerTests : PostgresTestBase
{
    [Test]
    public async Task Handle_should_return_empty_items_when_no_requests_exist()
    {
        using var provider = CreateProvider();

        var response = await SendAsync(provider);

        Assert.Multiple(() =>
        {
            Assert.That(response.Items, Is.Empty);
            Assert.That(response.TotalCount, Is.Zero);
            Assert.That(response.Page, Is.EqualTo(1));
            Assert.That(response.PageSize, Is.EqualTo(10));
        });
    }

    [Test]
    public async Task Handle_should_return_all_requests_with_mapped_information()
    {
        // Arrange
        var pending = await SeedRequestAsync(
            SellerRequestStatus.Pending,
            "Pending application.");

        var approved = await SeedRequestAsync(
            SellerRequestStatus.Approved,
            "Approved application.");

        var rejected = await SeedRequestAsync(
            SellerRequestStatus.Rejected,
            "Rejected application.");

        using var provider = CreateProvider();

        // Act
        var response = await SendAsync(provider);

        // Assert
        Assert.That(response.Items, Has.Count.EqualTo(3));
        Assert.That(response.TotalCount, Is.EqualTo(3));

        var pendingItem = response.Items.Single(x => x.Id == pending.Id);
        var approvedItem = response.Items.Single(x => x.Id == approved.Id);
        var rejectedItem = response.Items.Single(x => x.Id == rejected.Id);

        Assert.Multiple(() =>
        {
            Assert.That(pendingItem.UserId, Is.EqualTo(pending.UserId));
            Assert.That(pendingItem.Status, Is.EqualTo(SellerRequestStatus.Pending));
            Assert.That(pendingItem.Reason, Is.EqualTo(pending.Reason));
            Assert.That(pendingItem.ReviewedByUserId, Is.Null);
            Assert.That(pendingItem.ReviewedAt, Is.Null);

            Assert.That(approvedItem.Status, Is.EqualTo(SellerRequestStatus.Approved));
            Assert.That(approvedItem.ReviewedByUserId, Is.EqualTo(approved.ReviewedByUserId));
            Assert.That(approvedItem.ReviewedAt, Is.EqualTo(NormalizePostgresTimestamp(
                approved.ReviewedAt)));

            Assert.That(rejectedItem.Status, Is.EqualTo(SellerRequestStatus.Rejected));
            Assert.That(rejectedItem.ReviewedByUserId, Is.EqualTo(rejected.ReviewedByUserId));
            Assert.That(rejectedItem.ReviewedAt, Is.EqualTo(NormalizePostgresTimestamp(
                    rejected.ReviewedAt)));
        });
    }

    [Test]
    public async Task Handle_should_filter_requests_by_status()
    {
        // Arrange
        await SeedRequestAsync(SellerRequestStatus.Pending, "Pending 1");
        await SeedRequestAsync(SellerRequestStatus.Pending, "Pending 2");
        await SeedRequestAsync(SellerRequestStatus.Approved, "Approved");
        await SeedRequestAsync(SellerRequestStatus.Rejected, "Rejected");

        using var provider = CreateProvider();

        // Act
        var response = await SendAsync(
            provider,
            new GetSellerRequestsQuery(
                Status: SellerRequestStatus.Pending));

        // Assert
        Assert.That(response.Items, Has.Count.EqualTo(2));
        Assert.That(response.TotalCount, Is.EqualTo(2));

        Assert.That(
            response.Items.All(x => x.Status == SellerRequestStatus.Pending),
            Is.True);
    }

    [Test]
    public async Task Handle_should_apply_pagination_and_preserve_total_count()
    {
        // Arrange
        await SeedRequestAsync(SellerRequestStatus.Pending, "Request 1");
        await SeedRequestAsync(SellerRequestStatus.Pending, "Request 2");
        await SeedRequestAsync(SellerRequestStatus.Pending, "Request 3");
        await SeedRequestAsync(SellerRequestStatus.Approved, "Approved");

        Guid expectedId;

        await using (var context = CreateDbContext())
        {
            expectedId = await context.SellerRequests
                .Where(x => x.Status == SellerRequestStatus.Pending)
                .OrderByDescending(x => x.Id)
                .Skip(1)
                .Select(x => x.Id)
                .FirstAsync();
        }

        using var provider = CreateProvider();

        // Act
        var response = await SendAsync(
            provider,
            new GetSellerRequestsQuery(
                Page: 2,
                PageSize: 1,
                Status: SellerRequestStatus.Pending));

        // Assert
        Assert.That(response.Items, Has.Count.EqualTo(1));
        Assert.That(response.Items[0].Id, Is.EqualTo(expectedId));
        Assert.That(response.TotalCount, Is.EqualTo(3));
        Assert.That(response.Page, Is.EqualTo(2));
        Assert.That(response.PageSize, Is.EqualTo(1));
    }

    [SetUp]
    public async Task CleanSellerRequests()
    {
        await using var context = CreateDbContext();

        await context.SellerRequests.ExecuteDeleteAsync();
    }

    private static DateTime? NormalizePostgresTimestamp(DateTime? value)
    {
        if (value is null)
            return null;

        var ticks = value.Value.Ticks;

        return new DateTime(
            ticks - ticks % 10,
            value.Value.Kind);
    }

    private async Task<SellerRequest> SeedRequestAsync(
        SellerRequestStatus status,
        string reason)
    {
        var userId = Guid.NewGuid();

        var user = CreateUser(userId);

        var request = SellerRequest.Create(userId, reason);

        await using var context = CreateDbContext();

        context.Users.Add(user);

        if (status != SellerRequestStatus.Pending)
        {
            var admin = CreateUser(Guid.NewGuid());

            context.Users.Add(admin);

            if (status == SellerRequestStatus.Approved)
                request.Approve(admin.Id);
            else if (status == SellerRequestStatus.Rejected)
                request.Reject(admin.Id);
            else
                throw new ArgumentOutOfRangeException(nameof(status));
        }

        context.SellerRequests.Add(request);

        await context.SaveChangesAsync();

        return request;
    }

    private static ApplicationUser CreateUser(Guid id)
    {
        var suffix = Guid.NewGuid().ToString("N");

        return new ApplicationUser
        {
            Id = id,
            UserName = $"seller-request-{suffix}",
            Email = $"seller-request-{suffix}@test.local",
            EmailConfirmed = true,
            FirstName = "Test",
            LastName = "User",
            SellerStatus = SellerStatus.None
        };
    }

    private ServiceProvider CreateProvider()
    {
        var configuration = new ConfigurationManager();

        configuration["ConnectionStrings:ECommerceDb"] = ConnectionString;
        configuration["Jwt:Issuer"] = "ECommerce.Tests";
        configuration["Jwt:Audience"] = "ECommerce.Tests";
        configuration["Jwt:ExpirationMinutes"] = "60";
        configuration["Jwt:SecretKey"] = new string('t', 32);

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        return services.BuildServiceProvider();
    }

    private static async Task<GetSellerRequestsResponse> SendAsync(
        IServiceProvider provider,
        GetSellerRequestsQuery? query = null)
    {
        using var scope = provider.CreateScope();

        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        return await sender.Send(query ?? new GetSellerRequestsQuery());
    }
}
