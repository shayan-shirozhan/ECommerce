using ECommerce.API.Payments;
using ECommerce.Domain.Enums;
using Microsoft.AspNetCore.WebUtilities;

namespace ECommerce.Tests.API.Payments;

public sealed class PaymentResultRedirectBuilderTests
{
    [TestCase(PaymentStatus.Successful, "success")]
    [TestCase(PaymentStatus.Pending, "pending")]
    [TestCase(PaymentStatus.Failed, "failed")]
    [TestCase(PaymentStatus.Cancelled, "failed")]
    [TestCase(PaymentStatus.RequiresManualReview, "manual_review")]
    [TestCase(PaymentStatus.Refunded, "refunded")]
    public void Build_should_include_order_id_and_frontend_status(
        PaymentStatus paymentStatus,
        string expectedStatus)
    {
        var orderId = Guid.NewGuid();

        var result = PaymentResultRedirectBuilder.Build(
            "https://frontend.example.test/payment-result?source=checkout#result",
            orderId,
            paymentStatus);

        var uri = new Uri(result);
        var query = QueryHelpers.ParseQuery(uri.Query);

        Assert.Multiple(() =>
        {
            Assert.That(uri.Host, Is.EqualTo("frontend.example.test"));
            Assert.That(uri.Fragment, Is.EqualTo("#result"));
            Assert.That(query["source"], Is.EqualTo("checkout"));
            Assert.That(query["orderId"], Is.EqualTo(orderId.ToString("D")));
            Assert.That(query["status"], Is.EqualTo(expectedStatus));
        });
    }

    [TestCase("javascript:alert(1)")]
    [TestCase("/relative/path")]
    [TestCase("https://user:password@frontend.example.test/payment-result")]
    public void Build_should_reject_unsafe_or_non_absolute_urls(string url)
    {
        Assert.Throws<InvalidOperationException>(
            () => PaymentResultRedirectBuilder.Build(url, Guid.NewGuid(), PaymentStatus.Pending));
    }
}
