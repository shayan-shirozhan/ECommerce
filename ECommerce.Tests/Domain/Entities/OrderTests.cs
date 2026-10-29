using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Domain.Exceptions;

namespace ECommerce.Tests.Domain.Entities;

public sealed class OrderTests
{
    [Test]
    public void InitiatePayment_should_create_payment_and_move_order_to_awaiting_payment()
    {
        // Arrange
        var order = CreateOrderWithItem();

        // Act
        var payment = order.InitiatePayment();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(payment.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(payment.OrderId, Is.EqualTo(order.Id));
            Assert.That(payment.Amount, Is.EqualTo(200m));
            Assert.That(payment.PaymentMethod, Is.EqualTo(PaymentMethod.Mock));
            Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Pending));

            Assert.That(order.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(order.Payments, Does.Contain(payment));
            Assert.That(order.Payments, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void InitiatePayment_should_reject_order_that_is_not_pending()
    {
        // Arrange
        var order = CreateOrderWithItem();
        var firstPayment = order.InitiatePayment();

        // Act
        var exception = Assert.Throws<DomainException>(
            () => order.InitiatePayment());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(order.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(order.Payments, Has.Count.EqualTo(1));
            Assert.That(order.Payments.Single(), Is.SameAs(firstPayment));
        });
    }

    [Test]
    public void InitiatePayment_should_reject_zero_amount_without_changing_order()
    {
        // Arrange
        var order = CreateEmptyOrder();

        // Act
        var exception = Assert.Throws<DomainException>(
            () => order.InitiatePayment());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(order.TotalAmount, Is.Zero);
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Pending));
            Assert.That(order.Payments, Is.Empty);
        });
    }

    private static Order CreateOrder()
    {
        return Order.Create(
            Guid.NewGuid(),
            "Test Recipient",
            "09120000000",
            "Tehran",
            "Tehran",
            "1234567890",
            "Test Address");
    }

    private static Order CreateOrderWithItem()
    {
        var order = CreateOrder();

        order.AddItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Product",
            "Default Variant",
            "Test Seller",
            200m,
            1,
            0m);

        return order;
    }

    private static Order CreateEmptyOrder()
    {
        return CreateOrder();
    }
}
