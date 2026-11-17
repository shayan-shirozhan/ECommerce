namespace ECommerce.Infrastructure.Payments;

public sealed class ZarinPalOptions
{
    public const string SectionName = "ZarinPal";

    public bool Enabled { get; set; }

    public string MerchantId { get; set; } = string.Empty;

    /// <summary>
    /// Root URL for the ZarinPal v4 API, for example https://sandbox.zarinpal.com.
    /// </summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Payment page root, for example https://sandbox.zarinpal.com/pg/StartPay.
    /// </summary>
    public string PaymentPageBaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Public API callback URL, for example https://api.example.com/api/payments/zarinpal/callback.
    /// PaymentId is appended by the gateway adapter.
    /// </summary>
    public string CallbackUrl { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 15;
}
