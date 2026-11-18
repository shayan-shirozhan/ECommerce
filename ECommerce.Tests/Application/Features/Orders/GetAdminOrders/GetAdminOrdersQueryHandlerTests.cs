using ECommerce.Application;
using ECommerce.Application.Features.Orders.GetAdminOrders;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Orders.GetAdminOrders;

public sealed class GetAdminOrdersQueryHandlerTests : PostgresTestBase
{
    [SetUp]
    public async Task CleanOrders()
    {
        await using var context = CreateDbContext();
        await context.Payments.ExecuteDeleteAsync();
        await context.Orders.ExecuteDeleteAsync();
    }

    [Test]
    public async Task Handle_should_return_empty_page_when_no_orders_exist()
    {
        using var provider = CreateProvider();

        var response = await SendAsync(provider);

        Assert.Multiple(() =>
        {
            Assert.That(response.Items, Is.Empty);
            Assert.That(response.Page, Is.EqualTo(1));
            Assert.That(response.PageSize, Is.EqualTo(10));
            Assert.That(response.TotalCount, Is.Zero);
        });
    }

    [Test]
    public async Task Handle_should_return_customer_details_and_paginate_by_created_at_descending()
    {
        var firstSellerId = Guid.NewGuid();
        var middleSellerId = Guid.NewGuid();
        var newestSellerId = Guid.NewGuid();
        var firstBuyerId = Guid.NewGuid();
        var secondBuyerId = Guid.NewGuid();
        var thirdBuyerId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");

        var oldest = await SeedOrderAsync(
            firstBuyerId,
            $"oldest-{suffix}@example.test",
            "Old",
            "Buyer",
            firstSellerId,
            OrderStatus.Pending,
            new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc));

        var middle = await SeedOrderAsync(
            secondBuyerId,
            $"middle-{suffix}@example.test",
            "Middle",
            "Buyer",
            middleSellerId,
            OrderStatus.Pending,
            new DateTime(2026, 11, 2, 10, 0, 0, DateTimeKind.Utc));

        var newest = await SeedOrderAsync(
            thirdBuyerId,
            $"newest-{suffix}@example.test",
            "Newest",
            "Buyer",
            newestSellerId,
            OrderStatus.Pending,
            new DateTime(2026, 11, 3, 10, 0, 0, DateTimeKind.Utc));

        using var provider = CreateProvider();

        var response = await SendAsync(
            provider,
            new GetAdminOrdersQuery(Page: 2, PageSize: 1));

        Assert.Multiple(() =>
        {
            Assert.That(response.Items, Has.Count.EqualTo(1));
            Assert.That(response.Items[0].OrderId, Is.EqualTo(middle.Id));
            Assert.That(response.Items[0].UserId, Is.EqualTo(secondBuyerId));
            Assert.That(response.Items[0].CustomerEmail, Is.EqualTo($"middle-{suffix}@example.test"));
            Assert.That(response.Items[0].CustomerFirstName, Is.EqualTo("Middle"));
            Assert.That(response.Items[0].CustomerLastName, Is.EqualTo("Buyer"));
            Assert.That(response.Items[0].OrderGroupCount, Is.EqualTo(1));
            Assert.That(response.Items[0].ItemCount, Is.EqualTo(1));
            Assert.That(response.Page, Is.EqualTo(2));
            Assert.That(response.PageSize, Is.EqualTo(1));
            Assert.That(response.TotalCount, Is.EqualTo(3));
        });

        Assert.That(
            new[] { oldest.Id, newest.Id },
            Does.Not.Contain(response.Items[0].OrderId));
    }

    [Test]
    public async Task Handle_should_filter_by_status_and_inclusive_utc_date_range()
    {
        var suffix = Guid.NewGuid().ToString("N");

        var beforeRange = await SeedOrderAsync(
            Guid.NewGuid(), $"before-{suffix}@example.test", "Before", "Buyer", Guid.NewGuid(),
            OrderStatus.Pending,
            new DateTime(2026, 11, 1, 23, 59, 59, DateTimeKind.Utc));

        var fromBoundary = await SeedOrderAsync(
            Guid.NewGuid(), $"from-{suffix}@example.test", "From", "Buyer", Guid.NewGuid(),
            OrderStatus.AwaitingPayment,
            new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc));

        var toBoundary = await SeedOrderAsync(
            Guid.NewGuid(), $"to-{suffix}@example.test", "To", "Buyer", Guid.NewGuid(),
            OrderStatus.AwaitingPayment,
            new DateTime(2026, 11, 3, 23, 59, 59, DateTimeKind.Utc));

        var afterRange = await SeedOrderAsync(
            Guid.NewGuid(), $"after-{suffix}@example.test", "After", "Buyer", Guid.NewGuid(),
            OrderStatus.AwaitingPayment,
            new DateTime(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc));

        using var provider = CreateProvider();

        var response = await SendAsync(
            provider,
            new GetAdminOrdersQuery(
                Status: OrderStatus.AwaitingPayment,
                CreatedFromUtc: new DateTimeOffset(2026, 11, 2, 3, 30, 0, TimeSpan.FromHours(3.5)),
                CreatedToUtc: new DateTimeOffset(2026, 11, 4, 3, 29, 59, TimeSpan.FromHours(3.5))));

        Assert.Multiple(() =>
        {
            Assert.That(response.TotalCount, Is.EqualTo(2));
            Assert.That(response.Items.Select(item => item.OrderId),
                Is.EquivalentTo(new[] { fromBoundary.Id, toBoundary.Id }));
            Assert.That(response.Items.All(item => item.Status == OrderStatus.AwaitingPayment), Is.True);
            Assert.That(response.Items.Select(item => item.OrderId),
                Does.Not.Contain(beforeRange.Id));
            Assert.That(response.Items.Select(item => item.OrderId),
                Does.Not.Contain(afterRange.Id));
        });
    }

    private async Task<Order> SeedOrderAsync(
        Guid buyerId,
        string email,
        string firstName,
        string lastName,
        Guid sellerId,
        OrderStatus status,
        DateTime createdAtUtc)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var buyer = CreateUser(buyerId, $"buyer-{suffix}", email, firstName, lastName);
        var seller = CreateUser(sellerId, $"seller-{suffix}", $"seller-{suffix}@example.test", "Test", "Seller");
        var order = CreateOrder(buyerId, sellerId);

        if (status is OrderStatus.AwaitingPayment)
        {
            order.InitiatePayment(PaymentMethod.Mock);
        }
        else if (status is not OrderStatus.Pending)
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported status in this test helper.");
        }

        await using var context = CreateDbContext();
        context.Users.AddRange(buyer, seller);
        context.Orders.Add(order);
        context.Entry(order).Property(entity => entity.CreatedAt).CurrentValue = createdAtUtc;
        await context.SaveChangesAsync();

        return order;
    }

    private static Order CreateOrder(Guid userId, Guid sellerId)
    {
        var order = Order.Create(
            userId,
            "Test Buyer",
            "09120000000",
            "Tehran",
            "Tehran",
            "1234567890",
            "Test Street");

        order.AddItem(
            sellerId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Product",
            "Default",
            "Test Seller",
            100m,
            2,
            0m);

        return order;
    }

    private static ApplicationUser CreateUser(
        Guid id,
        string userName,
        string email,
        string firstName,
        string lastName)
    {
        return new ApplicationUser
        {
            Id = id,
            UserName = userName,
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            FirstName = firstName,
            LastName = lastName
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

    private static async Task<GetAdminOrdersResponse> SendAsync(
        IServiceProvider provider,
        GetAdminOrdersQuery? query = null)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(query ?? new GetAdminOrdersQuery());
    }
}
