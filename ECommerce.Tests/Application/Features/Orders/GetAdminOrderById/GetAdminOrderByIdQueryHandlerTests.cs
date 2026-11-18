using ECommerce.Application;
using ECommerce.Application.Exceptions;
using ECommerce.Application.Features.Orders.GetAdminOrderById;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Identity;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Orders.GetAdminOrderById;

public sealed class GetAdminOrderByIdQueryHandlerTests : PostgresTestBase
{
    [SetUp]
    public async Task CleanOrders()
    {
        await using var context = CreateDbContext();
        await context.Payments.ExecuteDeleteAsync();
        await context.Orders.ExecuteDeleteAsync();
    }

    [Test]
    public async Task Handle_should_return_customer_all_seller_groups_items_shipping_and_payment_summaries()
    {
        // Arrange
        var buyerId = Guid.NewGuid();
        var sellerAId = Guid.NewGuid();
        var sellerBId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var buyerEmail = $"buyer-{suffix}@example.test";

        var order = Order.Create(
            buyerId,
            "Shayan Buyer",
            "09120000000",
            "Tehran",
            "Tehran",
            "1234567890",
            "Test Street, No. 10");

        order.AddItem(
            sellerAId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "First Product", "Red / Large", "Seller A", 120m, 2, 20m);
        order.AddItem(
            sellerBId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "Second Product", "Default", "Seller B", 50m, 1, 0m);

        var payment = order.InitiatePayment(PaymentMethod.Mock);

        await using (var context = CreateDbContext())
        {
            context.Users.AddRange(
                CreateUser(buyerId, $"buyer-{suffix}", buyerEmail, "Shayan", "Buyer"),
                CreateUser(sellerAId, $"seller-a-{suffix}", $"seller-a-{suffix}@example.test", "Seller", "A"),
                CreateUser(sellerBId, $"seller-b-{suffix}", $"seller-b-{suffix}@example.test", "Seller", "B"));
            context.Orders.Add(order);
            await context.SaveChangesAsync();
        }

        using var provider = CreateProvider();

        // Act
        var response = await SendAsync(provider, new GetAdminOrderByIdQuery(order.Id));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.OrderId, Is.EqualTo(order.Id));
            Assert.That(response.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(response.TotalAmount, Is.EqualTo(270m));
            Assert.That(response.Customer.UserId, Is.EqualTo(buyerId));
            Assert.That(response.Customer.Email, Is.EqualTo(buyerEmail));
            Assert.That(response.Customer.FirstName, Is.EqualTo("Shayan"));
            Assert.That(response.Customer.LastName, Is.EqualTo("Buyer"));
            Assert.That(response.ShippingAddress.RecipientName, Is.EqualTo("Shayan Buyer"));
            Assert.That(response.ShippingAddress.AddressLine, Is.EqualTo("Test Street, No. 10"));
            Assert.That(response.Groups, Has.Count.EqualTo(2));
            Assert.That(response.Payments, Has.Count.EqualTo(1));
        });

        var firstGroup = response.Groups.Single(group => group.SellerId == sellerAId);
        var secondGroup = response.Groups.Single(group => group.SellerId == sellerBId);

        Assert.Multiple(() =>
        {
            Assert.That(firstGroup.SellerName, Is.EqualTo("Seller A"));
            Assert.That(firstGroup.TotalAmount, Is.EqualTo(220m));
            Assert.That(firstGroup.Items, Has.Count.EqualTo(1));
            Assert.That(firstGroup.Items[0].ProductName, Is.EqualTo("First Product"));
            Assert.That(firstGroup.Items[0].DiscountAmount, Is.EqualTo(20m));
            Assert.That(secondGroup.SellerName, Is.EqualTo("Seller B"));
            Assert.That(secondGroup.TotalAmount, Is.EqualTo(50m));
            Assert.That(secondGroup.Items, Has.Count.EqualTo(1));
            Assert.That(response.Payments[0].PaymentId, Is.EqualTo(payment.Id));
            Assert.That(response.Payments[0].PaymentMethod, Is.EqualTo(PaymentMethod.Mock));
            Assert.That(response.Payments[0].Status, Is.EqualTo(PaymentStatus.Pending));
            Assert.That(response.Payments[0].Amount, Is.EqualTo(270m));
        });
    }

    [Test]
    public void Handle_should_throw_not_found_for_unknown_order()
    {
        using var provider = CreateProvider();

        Assert.ThrowsAsync<NotFoundException>(async () =>
            await SendAsync(provider, new GetAdminOrderByIdQuery(Guid.NewGuid())));
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

    private static async Task<GetAdminOrderByIdResponse> SendAsync(
        IServiceProvider provider,
        GetAdminOrderByIdQuery query)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(query);
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
}
