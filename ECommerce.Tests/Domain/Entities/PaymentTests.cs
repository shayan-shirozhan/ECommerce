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


    [Test]
    public void ResumeManualReviewForVerification_should_reopen_payment_without_resetting_verification_history()
    {
        var order = CreateOrder();
        var payment = order.InitiatePayment(PaymentMethod.ZarinPal);
        payment.SetAuthority("A-authority");
        var firstAttempt = DateTime.UtcNow.AddMinutes(-15);
        var lastAttempt = DateTime.UtcNow.AddMinutes(-3);
        payment.MarkVerificationAttempted(firstAttempt);
        payment.MarkVerificationAttempted(lastAttempt);
        payment.MarkRequiresManualReview();

        payment.ResumeManualReviewForVerification();

        Assert.Multiple(() =>
        {
            Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Pending));
            Assert.That(payment.VerificationAttemptCount, Is.EqualTo(2));
            Assert.That(payment.FirstVerificationAttemptedAt, Is.EqualTo(firstAttempt));
            Assert.That(payment.LastVerificationAttemptedAt, Is.EqualTo(lastAttempt));
            Assert.That(payment.ManualReviewRequiredAt, Is.Null);
        });
    }

    [Test]
    public void ApproveManualReview_should_require_positive_reference_and_mark_payment_successful()
    {
        var order = CreateOrder();
        var payment = order.InitiatePayment(PaymentMethod.ZarinPal);
        payment.SetAuthority("A-authority");
        payment.MarkVerificationAttempted(DateTime.UtcNow.AddMinutes(-3));
        payment.RecordVerificationOutcome("503");
        payment.MarkRequiresManualReview();

        payment.ApproveManualReview(123456789);

        Assert.Multiple(() =>
        {
            Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Successful));
            Assert.That(payment.RefId, Is.EqualTo(123456789));
            Assert.That(payment.PaidAt, Is.Not.Null);
            Assert.That(payment.LastVerificationErrorCode, Is.Null);
        });
    }

    [Test]
    public void MarkSuccessful_should_clear_the_previous_verification_error_code()
    {
        var order = CreateOrder();
        var payment = order.InitiatePayment(PaymentMethod.ZarinPal);
        payment.SetAuthority("A-authority");
        payment.MarkVerificationAttempted(DateTime.UtcNow.AddMinutes(-3));
        payment.RecordVerificationOutcome("503");

        payment.MarkSuccessful(refId: 123456789);

        Assert.Multiple(() =>
        {
            Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Successful));
            Assert.That(payment.LastVerificationErrorCode, Is.Null);
        });
    }

    [Test]
    public void RejectManualReview_should_mark_payment_failed()
    {
        var order = CreateOrder();
        var payment = order.InitiatePayment(PaymentMethod.ZarinPal);
        payment.SetAuthority("A-authority");
        payment.MarkVerificationAttempted(DateTime.UtcNow.AddMinutes(-3));
        payment.MarkRequiresManualReview();

        payment.RejectManualReview();

        Assert.Multiple(() =>
        {
            Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Failed));
            Assert.That(payment.FailedAt, Is.Not.Null);
        });
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
