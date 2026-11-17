using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Infrastructure.Payments;

internal sealed class ZarinPalPaymentGateway(
    HttpClient httpClient,
    IOptions<ZarinPalOptions> options,
    ILogger<ZarinPalPaymentGateway> logger) : IPaymentGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ZarinPalOptions _options = options.Value;

    public string GetPaymentUrl(string authority)
    {
        if (string.IsNullOrWhiteSpace(authority))
            throw new ArgumentException("Payment authority is required.", nameof(authority));

        return $"{_options.PaymentPageBaseUrl.TrimEnd('/')}/{Uri.EscapeDataString(authority.Trim())}";
    }

    public async Task<PaymentGatewayRequestResult> RequestPaymentAsync(
        Guid paymentId,
        Guid orderId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var amountInRials = ConvertToRials(amount);
        var callbackUrl = BuildCallbackUrl(paymentId);

        var payload = new
        {
            merchant_id = _options.MerchantId,
            amount = amountInRials,
            callback_url = callbackUrl,
            description = $"ECommerce order {orderId}"
        };

        using var response = await SendAsync(
            "pg/v4/payment/request.json",
            payload,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var envelope = ParseEnvelope(body);

        if (response.IsSuccessStatusCode &&
            envelope.DataCode == 100 &&
            !string.IsNullOrWhiteSpace(envelope.Authority))
        {
            var paymentUrl = GetPaymentUrl(envelope.Authority);

            logger.LogInformation(
                "ZarinPal payment request created for payment {PaymentId}.",
                paymentId);

            return new PaymentGatewayRequestResult(
                true,
                envelope.Authority,
                paymentUrl);
        }

        logger.LogWarning(
            "ZarinPal rejected payment request for payment {PaymentId}. HTTP {StatusCode}, provider code {ProviderCode}.",
            paymentId,
            (int)response.StatusCode,
            envelope.ErrorCode ?? envelope.DataCode?.ToString(CultureInfo.InvariantCulture));

        return new PaymentGatewayRequestResult(
            false,
            null,
            null,
            envelope.ErrorCode ?? envelope.DataCode?.ToString(CultureInfo.InvariantCulture),
            envelope.ErrorMessage ?? "The payment provider rejected the payment request.");
    }

    public async Task<PaymentGatewayVerificationResult> VerifyPaymentAsync(
        decimal amount,
        string authority,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authority))
            throw new ArgumentException("Payment authority is required.", nameof(authority));

        var payload = new
        {
            merchant_id = _options.MerchantId,
            amount = ConvertToRials(amount),
            authority = authority.Trim()
        };

        using var response = await SendAsync(
            "pg/v4/payment/verify.json",
            payload,
            cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var envelope = ParseEnvelope(body);

        var isSuccessful = response.IsSuccessStatusCode &&
                           envelope.DataCode is 100 or 101 &&
                           envelope.RefId is > 0;

        if (isSuccessful)
        {
            logger.LogInformation(
                "ZarinPal payment verification succeeded. Provider code {ProviderCode}.",
                envelope.DataCode);
        }
        else
        {
            logger.LogWarning(
                "ZarinPal payment verification failed. HTTP {StatusCode}, provider code {ProviderCode}.",
                (int)response.StatusCode,
                envelope.ErrorCode ?? envelope.DataCode?.ToString(CultureInfo.InvariantCulture));
        }

        return new PaymentGatewayVerificationResult(
            isSuccessful,
            envelope.DataCode == 101,
            isSuccessful ? envelope.RefId : null,
            envelope.ErrorCode ?? envelope.DataCode?.ToString(CultureInfo.InvariantCulture),
            envelope.ErrorMessage ?? envelope.DataMessage);
    }

    private async Task<HttpResponseMessage> SendAsync(
        string relativePath,
        object payload,
        CancellationToken cancellationToken)
    {
        var requestUri = new Uri(
            $"{_options.ApiBaseUrl.TrimEnd('/')}/{relativePath}",
            UriKind.Absolute);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            };
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.UserAgent.ParseAdd("ECommerce.API/1.0");

            return await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new PaymentGatewayException(
                "The payment provider could not be reached. Please retry the payment.",
                exception);
        }
    }

    /// <summary>
    /// All application monetary values are stored in Toman. ZarinPal expects Rials,
    /// so conversion happens only at this external integration boundary.
    /// </summary>
    private static long ConvertToRials(decimal amount)
    {
        if (amount <= 0)
            throw new PaymentGatewayException("The payment amount must be greater than zero.");

        decimal amountInRials;

        try
        {
            amountInRials = checked(amount * 10m);
        }
        catch (OverflowException exception)
        {
            throw new PaymentGatewayException(
                "The payment amount is too large for the ZarinPal API.",
                exception);
        }

        if (amountInRials != decimal.Truncate(amountInRials) || amountInRials > long.MaxValue)
        {
            throw new PaymentGatewayException(
                "The payment amount cannot be represented as a whole number of Rials.");
        }

        return decimal.ToInt64(amountInRials);
    }

    private string BuildCallbackUrl(Guid paymentId)
    {
        var separator = _options.CallbackUrl.Contains('?') ? '&' : '?';
        return $"{_options.CallbackUrl}{separator}paymentId={Uri.EscapeDataString(paymentId.ToString("D"))}";
    }

    private static GatewayEnvelope ParseEnvelope(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new PaymentGatewayException(
                    "The payment provider returned an invalid response.");
            }

            var data = root.TryGetProperty("data", out var dataElement)
                ? dataElement
                : default;
            var errors = root.TryGetProperty("errors", out var errorsElement)
                ? errorsElement
                : default;

            return new GatewayEnvelope(
                TryGetInt32(data, "code"),
                TryGetString(data, "authority"),
                TryGetInt64(data, "ref_id"),
                TryGetInt32(errors, "code")?.ToString(CultureInfo.InvariantCulture),
                TryGetString(errors, "message"),
                TryGetString(data, "message"));
        }
        catch (JsonException exception)
        {
            throw new PaymentGatewayException(
                "The payment provider returned an invalid response.",
                exception);
        }
    }

    private static int? TryGetInt32(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static long? TryGetInt64(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var value)
            ? value
            : null;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private sealed record GatewayEnvelope(
        int? DataCode,
        string? Authority,
        long? RefId,
        string? ErrorCode,
        string? ErrorMessage,
        string? DataMessage);
}
