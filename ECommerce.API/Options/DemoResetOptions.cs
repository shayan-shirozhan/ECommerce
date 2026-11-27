namespace ECommerce.API.Options;

public sealed class DemoResetOptions
{
    public const string SectionName = "DemoReset";
    public const int MinimumIntervalSeconds = 60;

    public int IntervalSeconds { get; set; } = 86_400;
}
