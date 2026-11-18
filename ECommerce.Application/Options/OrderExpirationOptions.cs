namespace ECommerce.Application.Options;

public sealed class OrderExpirationOptions
{
    public const string SectionName = "OrderExpiration";

    public int PaymentWindowMinutes { get; set; } = 30;

    public int ScanIntervalSeconds { get; set; } = 60;

    public int ReconciliationIntervalSeconds { get; set; } = 120;

    public int MaxVerificationAttempts { get; set; } = 5;

    public int MaxVerificationAgeMinutes { get; set; } = 20;
}
