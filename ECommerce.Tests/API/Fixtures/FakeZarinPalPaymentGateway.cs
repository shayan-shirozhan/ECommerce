using System.Collections.Concurrent;
using ECommerce.Application.Abstractions.Payments;

namespace ECommerce.Tests.API.Fixtures;

public sealed class FakeZarinPalPaymentGateway : IPaymentGateway
{
    private readonly ConcurrentQueue<PaymentGatewayVerificationResult> _verificationResults = new();
    private readonly ConcurrentDictionary<string, long> _referenceIds = new(StringComparer.Ordinal);
    private long _nextReferenceId = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000;
    private int _verificationCount;
    private int _requestCount;

    public int VerificationCount => Volatile.Read(ref _verificationCount);
    public int RequestCount => Volatile.Read(ref _requestCount);

    public string GetAuthority(Guid paymentId) => $"AUTH-{paymentId:N}";

    public string GetPaymentUrl(string authority)
        => $"https://gateway.test/pay/{Uri.EscapeDataString(authority)}";

    public void EnqueueVerification(PaymentGatewayVerificationResult result)
        => _verificationResults.Enqueue(result);

    public void Reset()
    {
        while (_verificationResults.TryDequeue(out _))
        {
        }

        Interlocked.Exchange(ref _verificationCount, 0);
        Interlocked.Exchange(ref _requestCount, 0);
    }

    public Task<PaymentGatewayRequestResult> RequestPaymentAsync(
        Guid paymentId,
        Guid orderId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _requestCount);

        var authority = GetAuthority(paymentId);
        return Task.FromResult(new PaymentGatewayRequestResult(
            IsSuccessful: true,
            Authority: authority,
            PaymentUrl: GetPaymentUrl(authority)));
    }

    public Task<PaymentGatewayVerificationResult> VerifyPaymentAsync(
        decimal amount,
        string authority,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Interlocked.Increment(ref _verificationCount);

        if (_verificationResults.TryDequeue(out var configuredResult))
            return Task.FromResult(configuredResult);

        // Keep a stable, unique RefId per authority. The database enforces uniqueness
        // on Payments.RefId, so a constant fake reference would make later successful
        // payments fail with a persistence conflict.
        var refId = _referenceIds.GetOrAdd(
            authority,
            _ => Interlocked.Increment(ref _nextReferenceId));

        return Task.FromResult(new PaymentGatewayVerificationResult(
            IsSuccessful: true,
            IsAlreadyVerified: false,
            RefId: refId));
    }
}
