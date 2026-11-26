using ECommerce.Infrastructure.Payments;

namespace ECommerce.Tests.Infrastructure.Payments;

public sealed class DemoPaymentGatewayTests
{
    [Test]
    public async Task ProcessAsync_should_succeed_deterministically_with_stable_payment_reference()
    {
        var gateway = new DemoPaymentGateway();
        var paymentId = Guid.NewGuid();

        var firstResult = await gateway.ProcessAsync(paymentId, 125_000m);
        var secondResult = await gateway.ProcessAsync(paymentId, 125_000m);

        Assert.Multiple(() =>
        {
            Assert.That(firstResult.IsSuccessful, Is.True);
            Assert.That(firstResult.TransactionId, Is.EqualTo($"demo-txn-{paymentId:N}"));
            Assert.That(firstResult.GatewayReference, Is.EqualTo("public-demo"));
            Assert.That(secondResult, Is.EqualTo(firstResult));
        });
    }

    [Test]
    public void ProcessAsync_should_reject_empty_payment_id()
    {
        var gateway = new DemoPaymentGateway();

        Assert.ThrowsAsync<ArgumentException>(async () =>
            await gateway.ProcessAsync(Guid.Empty, 125_000m));
    }

    [Test]
    public void ProcessAsync_should_reject_non_positive_amount()
    {
        var gateway = new DemoPaymentGateway();

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await gateway.ProcessAsync(Guid.NewGuid(), 0m));
    }

    [Test]
    public void ProcessAsync_should_observe_cancellation()
    {
        var gateway = new DemoPaymentGateway();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await gateway.ProcessAsync(Guid.NewGuid(), 125_000m, cancellationTokenSource.Token));
    }
}
