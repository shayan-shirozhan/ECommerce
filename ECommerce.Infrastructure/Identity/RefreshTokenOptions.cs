namespace ECommerce.Infrastructure.Identity;

public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshTokens";

    public int ExpirationDays { get; set; } = 30;
}
