using System.Net;
using System.Text;
using System.Text.Json;
using ECommerce.Application.Exceptions;
using ECommerce.Infrastructure.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ECommerce.Tests.Infrastructure.Payments;

public sealed class ZarinPalPaymentGatewayTests
{
    [Test]
    public async Task RequestPaymentAsync_should_convert_toman_to_rial_and_build_callback_and_payment_urls()
    {
        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""{"data":{"code":100,"authority":"A000000000000000000000000000000000"},"errors":[]}""")
        });
        var gateway = CreateGateway(handler);

        var result = await gateway.RequestPaymentAsync(paymentId, orderId, 1250m);

        using var document = JsonDocument.Parse(handler.LastRequestBody!);
        var root = document.RootElement;

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.Authority, Is.EqualTo("A000000000000000000000000000000000"));
            Assert.That(result.PaymentUrl, Does.EndWith("/A000000000000000000000000000000000"));
            Assert.That(handler.LastRequestUri!.AbsoluteUri,
                Is.EqualTo("https://sandbox.zarinpal.com/pg/v4/payment/request.json"));
            Assert.That(root.GetProperty("amount").GetInt64(), Is.EqualTo(12500));
            Assert.That(root.GetProperty("callback_url").GetString(), Does.Contain(paymentId.ToString("D")));
        });
    }


    [Test]
    public async Task RequestPaymentAsync_should_return_provider_validation_errors()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
        {
            Content = JsonContent("""{"data":{},"errors":{"code":-9,"message":"The amount must be at least 1000."}}""")
        });
        var gateway = CreateGateway(handler);

        var result = await gateway.RequestPaymentAsync(Guid.NewGuid(), Guid.NewGuid(), 100m);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccessful, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("-9"));
            Assert.That(result.ErrorMessage, Does.Contain("at least 1000"));
        });
    }

    [Test]
    public void RequestPaymentAsync_should_reject_non_object_provider_response()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("[]")
        });
        var gateway = CreateGateway(handler);

        Assert.ThrowsAsync<ECommerce.Application.Exceptions.PaymentGatewayException>(
            async () => await gateway.RequestPaymentAsync(Guid.NewGuid(), Guid.NewGuid(), 100m));
    }

    [TestCase(100, false)]
    [TestCase(101, true)]
    public async Task VerifyPaymentAsync_should_accept_first_and_duplicate_success_codes(
        int providerCode,
        bool alreadyVerified)
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent($"{{\"data\":{{\"code\":{providerCode},\"ref_id\":987654321}},\"errors\":[]}}")
        });
        var gateway = CreateGateway(handler);

        var result = await gateway.VerifyPaymentAsync(1000m, "A-authority");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccessful, Is.True);
            Assert.That(result.IsAlreadyVerified, Is.EqualTo(alreadyVerified));
            Assert.That(result.RefId, Is.EqualTo(987654321));
        });

        using var document = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.That(
            document.RootElement.GetProperty("amount").GetInt64(),
            Is.EqualTo(10000));
        Assert.That(document.RootElement.GetProperty("authority").GetString(), Is.EqualTo("A-authority"));
    }

    [Test]
    public void RequestPaymentAsync_should_reject_amount_that_cannot_be_represented_as_whole_rials()
    {
        var handler = new RecordingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent("""{"data":{"code":100,"authority":"A-authority"},"errors":[]}""")
        });
        var gateway = CreateGateway(handler);

        var exception = Assert.ThrowsAsync<PaymentGatewayException>(
            async () => await gateway.RequestPaymentAsync(Guid.NewGuid(), Guid.NewGuid(), 100.01m));

        Assert.That(exception!.Message, Does.Contain("whole number of Rials"));
        Assert.That(handler.LastRequestUri, Is.Null);
    }
    private static ZarinPalPaymentGateway CreateGateway(
        RecordingHttpMessageHandler handler)
    {
        var client = new HttpClient(handler);
        var options = Options.Create(new ZarinPalOptions
        {
            Enabled = true,
            MerchantId = "11111111-1111-1111-1111-111111111111",
            ApiBaseUrl = "https://sandbox.zarinpal.com",
            PaymentPageBaseUrl = "https://sandbox.zarinpal.com/pg/StartPay",
            CallbackUrl = "https://localhost:5001/api/payments/zarinpal/callback",
        });

        return new ZarinPalPaymentGateway(
            client,
            options,
            NullLogger<ZarinPalPaymentGateway>.Instance);
    }

    private static StringContent JsonContent(string json)
        => new(json, Encoding.UTF8, "application/json");

    private sealed class RecordingHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return responseFactory(request);
        }
    }
}
