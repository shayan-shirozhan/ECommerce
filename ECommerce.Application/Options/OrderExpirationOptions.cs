namespace ECommerce.Application.Options;

public sealed class OrderExpirationOptions
{
    public const string SectionName = "OrderExpiration";

    public int PaymentWindowMinutes { get; set; } = 30;

    public int ScanIntervalSeconds { get; set; } = 60;
}
