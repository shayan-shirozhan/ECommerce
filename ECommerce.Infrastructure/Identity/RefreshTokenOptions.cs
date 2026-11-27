namespace ECommerce.Infrastructure.Identity;

public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshTokens";

    public int ExpirationDays { get; set; } = 30;
    public int RetentionDays { get; set; } = 30;
    public int CleanupIntervalHours { get; set; } = 24;
    public int RotationGracePeriodSeconds { get; set; } = 5;
}
