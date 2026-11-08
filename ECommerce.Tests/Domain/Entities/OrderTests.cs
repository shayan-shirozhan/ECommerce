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

    [Test]
    public void Cancel_should_cancel_pending_order_and_all_its_groups()
    {
        // Arrange
        var order = CreateOrderWithMultipleSellerGroups();
        var groups = order.Groups.ToList();

        Assert.That(groups, Has.Count.EqualTo(2));
        Assert.That(groups.Select(group => group.Status),
            Is.All.EqualTo(OrderGroupStatus.Pending));

        // Act
        order.Cancel();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Cancelled));
            Assert.That(groups.Select(group => group.Status),
                Is.All.EqualTo(OrderGroupStatus.Cancelled));
        });
    }

    [Test]
    public void Cancel_should_cancel_awaiting_payment_order_and_all_its_groups()
    {
        // Arrange
        var order = CreateOrderWithMultipleSellerGroups();
        order.InitiatePayment();
        var groups = order.Groups.ToList();

        // Act
        order.Cancel();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Cancelled));
            Assert.That(groups.Select(group => group.Status),
                Is.All.EqualTo(OrderGroupStatus.Cancelled));
        });
    }

    [Test]
    public void Cancel_should_reject_confirmed_order_without_changing_order_or_groups()
    {
        // Arrange
        var order = CreateOrderWithMultipleSellerGroups();
        order.InitiatePayment();
        order.Confirm();
        var groupStatusesBeforeCancellation = order.Groups
            .Select(group => group.Status)
            .ToArray();

        // Act
        var exception = Assert.Throws<DomainException>(() => order.Cancel());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Confirmed));
            Assert.That(order.Groups.Select(group => group.Status),
                Is.EqualTo(groupStatusesBeforeCancellation));
        });
    }

    [Test]
    public void Cancel_should_reject_processing_order_without_changing_order_or_groups()
    {
        // Arrange
        var (order, firstSellerId, secondSellerId) = CreateConfirmedOrderWithMultipleSellerGroups();
        order.MoveSellerGroupToProcessing(firstSellerId);

        var groups = order.Groups.ToList();
        var groupStatusesBeforeCancellation = groups
            .Select(group => group.Status)
            .ToArray();

        // Act
        var exception = Assert.Throws<DomainException>(() => order.Cancel());

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Processing));
            Assert.That(groups.Select(group => group.Status),
                Is.EqualTo(groupStatusesBeforeCancellation));
            Assert.That(GetSellerGroup(order, firstSellerId).Status,
                Is.EqualTo(OrderGroupStatus.Processing));
            Assert.That(GetSellerGroup(order, secondSellerId).Status,
                Is.EqualTo(OrderGroupStatus.Pending));
        });
    }

    [Test]
    public void MoveSellerGroupToProcessing_should_update_only_selected_group_and_set_order_to_processing()
    {
        // Arrange
        var (order, firstSellerId, secondSellerId) = CreateConfirmedOrderWithMultipleSellerGroups();

        // Act
        order.MoveSellerGroupToProcessing(firstSellerId);

        var firstGroup = GetSellerGroup(order, firstSellerId);
        var secondGroup = GetSellerGroup(order, secondSellerId);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Processing));
            Assert.That(firstGroup.Status, Is.EqualTo(OrderGroupStatus.Processing));
            Assert.That(secondGroup.Status, Is.EqualTo(OrderGroupStatus.Pending));
        });
    }

    [Test]
    public void MoveSellerGroupToProcessing_should_allow_another_seller_to_start_after_order_becomes_processing()
    {
        // Arrange
        var (order, firstSellerId, secondSellerId) = CreateConfirmedOrderWithMultipleSellerGroups();

        // Act
        order.MoveSellerGroupToProcessing(firstSellerId);
        order.MoveSellerGroupToProcessing(secondSellerId);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Processing));
            Assert.That(GetSellerGroup(order, firstSellerId).Status,
                Is.EqualTo(OrderGroupStatus.Processing));
            Assert.That(GetSellerGroup(order, secondSellerId).Status,
                Is.EqualTo(OrderGroupStatus.Processing));
        });
    }

    [Test]
    public void MoveSellerGroupToShipped_should_keep_order_processing_until_all_groups_are_shipped()
    {
        // Arrange
        var (order, firstSellerId, secondSellerId) = CreateConfirmedOrderWithMultipleSellerGroups();
        order.MoveSellerGroupToProcessing(firstSellerId);
        order.MoveSellerGroupToProcessing(secondSellerId);

        // Act - only the first seller ships their group.
        order.MoveSellerGroupToShipped(firstSellerId);

        // Assert intermediate aggregate state.
        Assert.Multiple(() =>
        {
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Processing));
            Assert.That(GetSellerGroup(order, firstSellerId).Status,
                Is.EqualTo(OrderGroupStatus.Shipped));
            Assert.That(GetSellerGroup(order, secondSellerId).Status,
                Is.EqualTo(OrderGroupStatus.Processing));
        });

        // Act - the second seller ships their group.
        order.MoveSellerGroupToShipped(secondSellerId);

        // Assert final aggregate state.
        Assert.Multiple(() =>
        {
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Shipped));
            Assert.That(GetSellerGroup(order, firstSellerId).Status,
                Is.EqualTo(OrderGroupStatus.Shipped));
            Assert.That(GetSellerGroup(order, secondSellerId).Status,
                Is.EqualTo(OrderGroupStatus.Shipped));
        });
    }

    [Test]
    public void MoveSellerGroupToDelivered_should_keep_order_shipped_until_all_groups_are_delivered()
    {
        // Arrange
        var (order, firstSellerId, secondSellerId) = CreateConfirmedOrderWithMultipleSellerGroups();
        MoveBothGroupsToShipped(order, firstSellerId, secondSellerId);

        // Act - only the first seller's group is delivered.
        order.MoveSellerGroupToDelivered(firstSellerId);

        // Assert intermediate aggregate state.
        Assert.Multiple(() =>
        {
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Shipped));
            Assert.That(GetSellerGroup(order, firstSellerId).Status,
                Is.EqualTo(OrderGroupStatus.Delivered));
            Assert.That(GetSellerGroup(order, secondSellerId).Status,
                Is.EqualTo(OrderGroupStatus.Shipped));
        });

        // Act - the second seller's group is delivered.
        order.MoveSellerGroupToDelivered(secondSellerId);

        // Assert final aggregate state.
        Assert.Multiple(() =>
        {
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Delivered));
            Assert.That(order.Groups.Select(group => group.Status),
                Is.All.EqualTo(OrderGroupStatus.Delivered));
        });
    }

    [Test]
    public void MoveSellerGroupToProcessing_should_reject_seller_without_a_group_without_changing_order()
    {
        // Arrange
        var (order, firstSellerId, secondSellerId) = CreateConfirmedOrderWithMultipleSellerGroups();
        var unknownSellerId = Guid.NewGuid();

        // Act
        var exception = Assert.Throws<DomainException>(
            () => order.MoveSellerGroupToProcessing(unknownSellerId));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Confirmed));
            Assert.That(GetSellerGroup(order, firstSellerId).Status,
                Is.EqualTo(OrderGroupStatus.Pending));
            Assert.That(GetSellerGroup(order, secondSellerId).Status,
                Is.EqualTo(OrderGroupStatus.Pending));
        });
    }

    [Test]
    public void MoveSellerGroupToShipped_should_reject_group_that_is_not_processing_without_partial_changes()
    {
        // Arrange
        var (order, firstSellerId, secondSellerId) = CreateConfirmedOrderWithMultipleSellerGroups();
        order.MoveSellerGroupToProcessing(firstSellerId);

        // Act - the order is Processing, but the second seller's group is still Pending.
        var exception = Assert.Throws<DomainException>(
            () => order.MoveSellerGroupToShipped(secondSellerId));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Processing));
            Assert.That(GetSellerGroup(order, firstSellerId).Status,
                Is.EqualTo(OrderGroupStatus.Processing));
            Assert.That(GetSellerGroup(order, secondSellerId).Status,
                Is.EqualTo(OrderGroupStatus.Pending));
        });
    }

    [Test]
    public void MoveSellerGroupToDelivered_should_reject_group_that_is_already_delivered_without_changing_order()
    {
        // Arrange
        var (order, firstSellerId, secondSellerId) = CreateConfirmedOrderWithMultipleSellerGroups();
        MoveBothGroupsToShipped(order, firstSellerId, secondSellerId);
        order.MoveSellerGroupToDelivered(firstSellerId);

        // Act - the first group is already Delivered; it cannot be delivered twice.
        var exception = Assert.Throws<DomainException>(
            () => order.MoveSellerGroupToDelivered(firstSellerId));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Shipped));
            Assert.That(GetSellerGroup(order, firstSellerId).Status,
                Is.EqualTo(OrderGroupStatus.Delivered));
            Assert.That(GetSellerGroup(order, secondSellerId).Status,
                Is.EqualTo(OrderGroupStatus.Shipped));
        });
    }

    [Test]
    public void MoveSellerGroupToProcessing_should_reject_order_that_has_not_been_confirmed()
    {
        // Arrange
        var order = CreateOrderWithMultipleSellerGroups();
        order.InitiatePayment();
        var groups = order.Groups.ToList();

        // Act
        var exception = Assert.Throws<DomainException>(
            () => order.MoveSellerGroupToProcessing(groups[0].SellerId));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(exception, Is.Not.Null);
            Assert.That(order.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
            Assert.That(groups.Select(group => group.Status),
                Is.All.EqualTo(OrderGroupStatus.Pending));
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

    private static Order CreateOrderWithMultipleSellerGroups()
    {
        var order = CreateOrder();

        order.AddItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "First Seller Product",
            "Default Variant",
            "First Seller",
            100m,
            1,
            0m);

        order.AddItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Second Seller Product",
            "Default Variant",
            "Second Seller",
            150m,
            1,
            0m);

        return order;
    }

    private static (Order Order, Guid FirstSellerId, Guid SecondSellerId)
        CreateConfirmedOrderWithMultipleSellerGroups()
    {
        var order = CreateOrder();
        var firstSellerId = Guid.NewGuid();
        var secondSellerId = Guid.NewGuid();

        order.AddItem(
            firstSellerId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "First Seller Product",
            "Default Variant",
            "First Seller",
            100m,
            1,
            0m);

        order.AddItem(
            secondSellerId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Second Seller Product",
            "Default Variant",
            "Second Seller",
            150m,
            1,
            0m);

        order.InitiatePayment();
        order.Confirm();

        return (order, firstSellerId, secondSellerId);
    }

    private static OrderGroup GetSellerGroup(Order order, Guid sellerId)
    {
        return order.Groups.Single(group => group.SellerId == sellerId);
    }

    private static void MoveBothGroupsToShipped(
        Order order,
        Guid firstSellerId,
        Guid secondSellerId)
    {
        order.MoveSellerGroupToProcessing(firstSellerId);
        order.MoveSellerGroupToProcessing(secondSellerId);
        order.MoveSellerGroupToShipped(firstSellerId);
        order.MoveSellerGroupToShipped(secondSellerId);
    }

    private static Order CreateEmptyOrder()
    {
        return CreateOrder();
    }
}
