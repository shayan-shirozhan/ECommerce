namespace ECommerce.Application.Abstractions.Payments;

public static class PaymentGatewayRetryPolicy
{
    // Gateway HTTP timeout is limited to 59 seconds; two minutes leaves a buffer
    // for the failed request to unwind and its result/state to be persisted.
    public static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(2);

    public static bool CanRetry(DateTime? lastAttemptAtUtc, DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Current time must be UTC.", nameof(nowUtc));

        return lastAttemptAtUtc is null || lastAttemptAtUtc.Value <= nowUtc.Subtract(RetryDelay);
    }
}
