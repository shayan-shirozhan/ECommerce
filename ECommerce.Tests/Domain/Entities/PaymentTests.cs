using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;

namespace ECommerce.Tests.Domain.Entities;

public sealed class PaymentTests
{
    [Test]
    public void MarkVerificationAttempted_should_track_first_last_attempt_and_count()
    {
        var order = CreateOrder();
        var payment = order.InitiatePayment(PaymentMethod.ZarinPal);
        var firstAttempt = DateTime.UtcNow.AddMinutes(-5);
        var secondAttempt = DateTime.UtcNow.AddMinutes(-2);

        payment.SetAuthority("A-authority");
        payment.MarkVerificationAttempted(firstAttempt);
        payment.MarkVerificationAttempted(secondAttempt);

        Assert.Multiple(() =>
        {
            Assert.That(payment.FirstVerificationAttemptedAt, Is.EqualTo(firstAttempt));
            Assert.That(payment.LastVerificationAttemptedAt, Is.EqualTo(secondAttempt));
            Assert.That(payment.VerificationAttemptCount, Is.EqualTo(2));
        });
    }

    [Test]
    public void HasReachedVerificationLimit_should_return_true_for_attempt_count_or_maximum_age()
    {
        var order = CreateOrder();
        var payment = order.InitiatePayment(PaymentMethod.ZarinPal);
        payment.SetAuthority("A-authority");
        var nowUtc = DateTime.UtcNow;

        payment.MarkVerificationAttempted(nowUtc.AddMinutes(-21));
        Assert.That(
            payment.HasReachedVerificationLimit(nowUtc, maxAttempts: 5, maxAge: TimeSpan.FromMinutes(20)),
            Is.True);

        var anotherOrder = CreateOrder();
        var anotherPayment = anotherOrder.InitiatePayment(PaymentMethod.ZarinPal);
        anotherPayment.SetAuthority("B-authority");
        for (var attempt = 0; attempt < 5; attempt++)
            anotherPayment.MarkVerificationAttempted(nowUtc.AddMinutes(-attempt * 3));

        Assert.That(
            anotherPayment.HasReachedVerificationLimit(nowUtc, maxAttempts: 5, maxAge: TimeSpan.FromMinutes(20)),
            Is.True);
    }

    private static Order CreateOrder()
    {
        var order = Order.Create(
            Guid.NewGuid(),
            "Test Buyer",
            "09120000000",
            "Tehran",
            "Tehran",
            "1234567890",
            "Test Street");

        order.AddItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Product",
            "Default",
            "Test Seller",
            200m,
            1,
            0m);

        return order;
    }
}
