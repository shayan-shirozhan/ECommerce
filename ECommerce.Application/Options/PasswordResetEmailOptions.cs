namespace ECommerce.Application.Options;

public sealed class PasswordResetEmailOptions
{
    public const string SectionName = "PasswordResetEmail";

    public string FrontendUrl { get; set; } = string.Empty;

    public int TokenLifespanMinutes { get; set; } = 30;
}
