namespace ECommerce.API.Options;

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    public string PaymentResultUrl { get; set; } = string.Empty;
}
