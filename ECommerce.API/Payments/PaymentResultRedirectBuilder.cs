using ECommerce.Domain.Enums;
using Microsoft.AspNetCore.WebUtilities;

namespace ECommerce.API.Payments;

public static class PaymentResultRedirectBuilder
{
    public static string Build(
        string paymentResultUrl,
        Guid orderId,
        PaymentStatus paymentStatus)
    {
        if (!Uri.TryCreate(paymentResultUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new InvalidOperationException(
                "Frontend:PaymentResultUrl must be an absolute HTTP or HTTPS URL.");
        }

        var status = paymentStatus switch
        {
            PaymentStatus.Successful => "success",
            PaymentStatus.Pending => "pending",
            PaymentStatus.RequiresManualReview => "manual_review",
            PaymentStatus.Failed or PaymentStatus.Cancelled => "failed",
            PaymentStatus.Refunded => "refunded",
            _ => "unknown"
        };

        return QueryHelpers.AddQueryString(
            paymentResultUrl,
            new Dictionary<string, string?>
            {
                ["orderId"] = orderId.ToString("D"),
                ["status"] = status
            });
    }
}
