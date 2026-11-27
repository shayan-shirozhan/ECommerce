using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ECommerce.API.BackgroundServices;
using ECommerce.Application.Abstractions.Payments;
using ECommerce.Application.Features.Orders.CreateOrderFromCart;
using ECommerce.Application.Features.Orders.GetMyOrderById;
using ECommerce.Application.Features.Payments.InitiatePayment;
using ECommerce.Application.Features.Payments.ResolveAdminPayment;
using ECommerce.Domain.Entities;
using ECommerce.Domain.Enums;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Tests.API.Fixtures;
using ECommerce.Tests.Infrastructure.Fixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerce.Tests.API;

[TestFixture]
[NonParallelizable]
public sealed class ApiEndToEndTests : PostgresTestBase
{
    private const string TestOrigin = "http://localhost";
    private const string AdminEmail = "admin@test.com";
    private const string SeededSellerEmail = "seller@test.com";
    private const string SeededPassword = "Password123!";
    private const string CustomerPassword = "P@ssword123!";

    private readonly Dictionary<string, string?> _originalBootstrapEnvironment = new(StringComparer.Ordinal);
    private ECommerceApiFactory _factory = null!;
    private HttpClient _client = null!;
    private FakeZarinPalPaymentGateway _paymentGateway = null!;
    private Guid _productVariantId;
    private Guid _seededSellerOfferId;
    private string _adminAccessToken = null!;
    private string _seededSellerAccessToken = null!;

    [OneTimeSetUp]
    public async Task StartApi()
    {
        // Program reads configuration during top-level startup, before the factory's
        // ConfigureAppConfiguration callback can override it. Seed those values first.
        ConfigureBootstrapEnvironment();

        _paymentGateway = new FakeZarinPalPaymentGateway();
        _factory = new ECommerceApiFactory(ConnectionString, _paymentGateway);
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
            BaseAddress = new Uri(TestOrigin)
        });

        _adminAccessToken = (await LoginAsync(AdminEmail, SeededPassword)).AccessToken;
        _seededSellerAccessToken = (await LoginAsync(SeededSellerEmail, SeededPassword)).AccessToken;

        Guid brandId;
        Guid categoryId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();
            var brand = Brand.Create($"API E2E Brand {Guid.NewGuid():N}");
            var category = Category.Create($"API E2E Category {Guid.NewGuid():N}");

            dbContext.Brands.Add(brand);
            dbContext.Categories.Add(category);
            await dbContext.SaveChangesAsync();

            brandId = brand.Id;
            categoryId = category.Id;
        }

        using var productRequest = CreateRequest(
            HttpMethod.Post,
            "/api/products",
            _adminAccessToken);

        productRequest.Content = JsonContent.Create(new
        {
            name = "API Integration Test Product",
            description = "Product created through the real API pipeline.",
            brandId,
            categoryId
        });

        using var productResponse =
            await _client.SendAsync(productRequest);

        Assert.That(productResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            await productResponse.Content.ReadAsStringAsync());
        var productId = await ReadIdAsync(productResponse);

        using (var activateProduct = CreateRequest(HttpMethod.Put, $"/api/products/{productId}/activate", _adminAccessToken))
        using (var activateResponse = await _client.SendAsync(activateProduct))
            Assert.That(activateResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        var variantResponse = await SendJsonAsync(
            HttpMethod.Post,
            "/api/product-variants",
            _adminAccessToken,
            new
            {
                productId,
                sku = $"E2E-{Guid.NewGuid():N}",
                name = "Default variant"
            });
        Assert.That(variantResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        _productVariantId = await ReadIdAsync(variantResponse);
        variantResponse.Dispose();

        using (var activateVariant = CreateRequest(
                   HttpMethod.Put,
                   $"/api/product-variants/{_productVariantId}/activate",
                   _adminAccessToken))
        using (var activateResponse = await _client.SendAsync(activateVariant))
            Assert.That(activateResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        _seededSellerOfferId = await CreateSellerOfferAsync(
            _seededSellerAccessToken,
            _productVariantId,
            stock: 1000);
    }

    [SetUp]
    public void ResetPaymentGateway()
    {
        _paymentGateway.Reset();
    }

    [Test]
    public async Task Demo_payment_endpoints_should_not_be_mapped_in_development()
    {
        using var response = await _client.PostAsync(
            $"/api/demo/payments/orders/{Guid.NewGuid()}/initiate",
            content: null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [OneTimeTearDown]
    public async Task StopApi()
    {
        try
        {
            _client?.Dispose();
            if (_factory is not null)
                await _factory.DisposeAsync();
        }
        finally
        {
            RestoreBootstrapEnvironment();
        }
    }

    private void ConfigureBootstrapEnvironment()
    {
        SetBootstrapEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
        SetBootstrapEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        SetBootstrapEnvironmentVariable("ConnectionStrings__ECommerceDb", ConnectionString);
        SetBootstrapEnvironmentVariable("Jwt__SecretKey", ECommerceApiFactory.TestJwtSigningKey);
        SetBootstrapEnvironmentVariable("Jwt__Issuer", "ECommerce.API");
        SetBootstrapEnvironmentVariable("Jwt__Audience", "ECommerce.API");
        SetBootstrapEnvironmentVariable("Jwt__ExpirationMinutes", "60");
        SetBootstrapEnvironmentVariable("RefreshTokens__ExpirationDays", "30");
        SetBootstrapEnvironmentVariable("RefreshTokens__RotationGracePeriodSeconds", "1");
        SetBootstrapEnvironmentVariable("ZarinPal__Enabled", "true");
        SetBootstrapEnvironmentVariable("ZarinPal__MerchantId", "11111111-1111-1111-1111-111111111111");
        SetBootstrapEnvironmentVariable("ZarinPal__ApiBaseUrl", "https://sandbox.zarinpal.com");
        SetBootstrapEnvironmentVariable("ZarinPal__PaymentPageBaseUrl", "https://sandbox.zarinpal.com/pg/StartPay");
        SetBootstrapEnvironmentVariable("ZarinPal__CallbackUrl", "https://api.test/api/payments/zarinpal/callback");
        SetBootstrapEnvironmentVariable("ZarinPal__TimeoutSeconds", "15");
        SetBootstrapEnvironmentVariable("Frontend__PaymentResultUrl", "https://frontend.test/payment-result");
        SetBootstrapEnvironmentVariable("HttpsRedirection__Enabled", "false");
        SetBootstrapEnvironmentVariable("Cors__AllowedOrigins__0", TestOrigin);
        SetBootstrapEnvironmentVariable("RateLimiting__Auth__PermitLimit", "10000");
        SetBootstrapEnvironmentVariable("RateLimiting__Auth__WindowSeconds", "60");
        SetBootstrapEnvironmentVariable("PasswordResetEmail__FrontendUrl", "https://frontend.test/reset-password");
        SetBootstrapEnvironmentVariable("PasswordResetEmail__TokenLifespanMinutes", "30");
        SetBootstrapEnvironmentVariable("OrderExpiration__PaymentWindowMinutes", "30");
        SetBootstrapEnvironmentVariable("OrderExpiration__ScanIntervalSeconds", "60");
        SetBootstrapEnvironmentVariable("OrderExpiration__ReconciliationIntervalSeconds", "120");
        SetBootstrapEnvironmentVariable("OrderExpiration__MaxVerificationAttempts", "5");
        SetBootstrapEnvironmentVariable("OrderExpiration__MaxVerificationAgeMinutes", "20");
    }

    private void SetBootstrapEnvironmentVariable(string name, string value)
    {
        if (!_originalBootstrapEnvironment.ContainsKey(name))
            _originalBootstrapEnvironment.Add(name, Environment.GetEnvironmentVariable(name));

        Environment.SetEnvironmentVariable(name, value);
    }

    private void RestoreBootstrapEnvironment()
    {
        foreach (var (name, value) in _originalBootstrapEnvironment)
            Environment.SetEnvironmentVariable(name, value);

        _originalBootstrapEnvironment.Clear();
    }


    [Test]
    public async Task AdminPaymentResolution_RequiresAdminRoleAndRejectsWithInventoryRestoration()
    {
        Guid paymentId;
        Guid orderId;
        int stockBeforeReservation;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();
            var admin = await dbContext.Users.SingleAsync(user => user.Email == AdminEmail);
            var seller = await dbContext.Users.SingleAsync(user => user.Email == SeededSellerEmail);
            var offer = await dbContext.SellerOffers
                .Include(entity => entity.ProductVariant)
                .ThenInclude(variant => variant.Product)
                .SingleAsync(entity => entity.Id == _seededSellerOfferId);

            stockBeforeReservation = offer.Stock;
            offer.UpdateStock(stockBeforeReservation - 2);

            var order = Order.Create(
                admin.Id,
                "Admin Resolution Test",
                "09120000000",
                "Tehran",
                "Tehran",
                "1234567890",
                "Admin resolution test address");
            order.AddItem(
                seller.Id,
                offer.ProductVariant.ProductId,
                offer.ProductVariantId,
                offer.Id,
                offer.ProductVariant.Product.Name,
                offer.ProductVariant.Name ?? "Default",
                "Seeded Seller",
                offer.Price,
                quantity: 2,
                discountAmount: 0m);

            var payment = order.InitiatePayment(PaymentMethod.ZarinPal);
            payment.SetAuthority($"A{Guid.NewGuid():N}");
            for (var attempt = 5; attempt >= 1; attempt--)
                payment.MarkVerificationAttempted(DateTime.UtcNow.AddMinutes(-attempt * 3));
            payment.MarkRequiresManualReview();

            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync();

            paymentId = payment.Id;
            orderId = order.Id;
        }

        // Seller credentials must not be accepted by the admin-only endpoint.
        using (var sellerRequest = CreateRequest(
                   HttpMethod.Post,
                   $"/api/admin/payments/{paymentId:D}/resolve",
                   _seededSellerAccessToken))
        {
            sellerRequest.Content = JsonContent.Create(new
            {
                action = (int)PaymentResolutionAction.RejectAndCancel,
                reason = "This request must be blocked for seller accounts."
            });

            using var sellerResponse = await _client.SendAsync(sellerRequest);
            Assert.That(sellerResponse.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        using (var listRequest = CreateRequest(
                   HttpMethod.Get,
                   "/api/admin/orders?hasManualReview=true",
                   _adminAccessToken))
        using (var listResponse = await _client.SendAsync(listRequest))
        {
            Assert.That(listResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var orders = await listResponse.Content.ReadFromJsonAsync<
                ECommerce.Application.Features.Orders.GetAdminOrders.GetAdminOrdersResponse>();
            Assert.That(orders!.Items.Any(item => item.OrderId == orderId), Is.True);
        }

        using (var adminRequest = CreateRequest(
                   HttpMethod.Post,
                   $"/api/admin/payments/{paymentId:D}/resolve",
                   _adminAccessToken))
        {
            adminRequest.Content = JsonContent.Create(new
            {
                action = (int)PaymentResolutionAction.RejectAndCancel,
                reason = "Provider settlement evidence confirms that no payment was collected."
            });

            using var adminResponse = await _client.SendAsync(adminRequest);
            Assert.That(
                adminResponse.StatusCode,
                Is.EqualTo(HttpStatusCode.OK),
                await adminResponse.Content.ReadAsStringAsync());

            var result = await adminResponse.Content.ReadFromJsonAsync<ResolveAdminPaymentResponse>();
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.PaymentStatus, Is.EqualTo(PaymentStatus.Failed));
            Assert.That(result.OrderStatus, Is.EqualTo(OrderStatus.Cancelled));
            Assert.That(result.RestoredQuantity, Is.EqualTo(2));
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();
            var offer = await dbContext.SellerOffers.SingleAsync(entity => entity.Id == _seededSellerOfferId);
            var order = await dbContext.Orders.Include(entity => entity.Payments)
                .SingleAsync(entity => entity.Id == orderId);
            var audit = await dbContext.PaymentResolutionAudits.SingleAsync(entity => entity.PaymentId == paymentId);

            Assert.Multiple(() =>
            {
                Assert.That(offer.Stock, Is.EqualTo(stockBeforeReservation));
                Assert.That(order.Status, Is.EqualTo(OrderStatus.Cancelled));
                Assert.That(order.Payments.Single().Status, Is.EqualTo(PaymentStatus.Failed));
                Assert.That(audit.AdminUserId, Is.Not.EqualTo(Guid.Empty));
                Assert.That(audit.Outcome, Is.EqualTo("RejectedAndCancelled"));
            });
        }
    }

    [Test]
    public async Task PurchaseLifecycle_CompletesRegistrationSellerApprovalCartPaymentAndDeliveryThroughHttp()
    {
        var customer = await RegisterAndLoginAsync("customer");
        var sellerApplicant = await RegisterAndLoginAsync("seller-applicant");

        var sellerRequestResponse = await SendJsonAsync(
            HttpMethod.Post,
            "/api/seller-requests",
            sellerApplicant.AccessToken,
            new { reason = "I want to sell products through the marketplace." });

        Assert.That(
            sellerRequestResponse.StatusCode,
            Is.EqualTo(HttpStatusCode.Created),
            await sellerRequestResponse.Content.ReadAsStringAsync());

        var sellerRequestId = await ReadIdAsync(sellerRequestResponse);
        sellerRequestResponse.Dispose();

        var admin = await LoginAsync(AdminEmail, SeededPassword);

        using (var approveRequest = CreateRequest(
                   HttpMethod.Post,
                   $"/api/seller-requests/{sellerRequestId}/approve",
                   admin.AccessToken))
        using (var approveResponse = await _client.SendAsync(approveRequest))
        {
            Assert.That(
                approveResponse.StatusCode,
                Is.EqualTo(HttpStatusCode.NoContent),
                await approveResponse.Content.ReadAsStringAsync());
        }

        // The access token issued before approval does not contain the new Seller role.
        var approvedSeller = await LoginAsync(
            sellerApplicant.Email,
            CustomerPassword);

        var sellerOfferId = await CreateSellerOfferAsync(
            approvedSeller.AccessToken,
            _productVariantId,
            stock: 20);

        await AddToCartAsync(
            customer.AccessToken,
            sellerOfferId,
            quantity: 1);

        var order = await CreateOrderAsync(customer.AccessToken);

        // Initiate payment.
        var payment = await InitiatePaymentAsync(
            customer.AccessToken,
            order.OrderId);

        Assert.That(payment.Status, Is.EqualTo(PaymentStatus.Pending));
        Assert.That(payment.PaymentMethod, Is.EqualTo(PaymentMethod.ZarinPal));
        Assert.That(_paymentGateway.RequestCount, Is.EqualTo(1));
        Assert.That(
            payment.RedirectUrl,
            Does.StartWith("https://gateway.test/pay/"));

        // Handle successful ZarinPal callback.
        var authority = _paymentGateway.GetAuthority(payment.PaymentId);

        using var callbackResponse = await _client.GetAsync(
            BuildCallbackUrl(
                payment.PaymentId,
                authority,
                "OK"));

        // Collect diagnostics before asserting the expected redirect.
        if (callbackResponse.StatusCode != HttpStatusCode.Redirect)
        {
            var responseBody =
                await callbackResponse.Content.ReadAsStringAsync();

            var paymentState =
                await ReadOrderStateAsync(order.OrderId);

            Assert.Fail(
                $"""
                 ZarinPal callback failed.

                 HTTP Status: {callbackResponse.StatusCode}
                 Response: {responseBody}

                 Order Status: {paymentState.Status}
                 Payment Status: {paymentState.PaymentStatus}
                 Verification Attempts: {paymentState.VerificationAttemptCount}
                 Gateway Verification Count: {_paymentGateway.VerificationCount}
                 """);
        }

        var redirectLocation = callbackResponse.Headers.Location;

        Assert.That(redirectLocation, Is.Not.Null);

        Assert.That(
            redirectLocation!.ToString(),
            Does.Contain($"orderId={order.OrderId:D}"));

        Assert.That(
            redirectLocation.ToString(),
            Does.Contain("status=success"));

        Assert.That(
            _paymentGateway.VerificationCount,
            Is.EqualTo(1));

        // Seller processes and ships the order.
        await UpdateSellerOrderStatusAsync(
            approvedSeller.AccessToken,
            order.OrderId,
            OrderGroupStatus.Processing);

        await UpdateSellerOrderStatusAsync(
            approvedSeller.AccessToken,
            order.OrderId,
            OrderGroupStatus.Shipped,
            shippingCarrier: "Test Carrier",
            trackingCode: "E2E-TRACK-0001");

        await UpdateSellerOrderStatusAsync(
            approvedSeller.AccessToken,
            order.OrderId,
            OrderGroupStatus.Delivered);

        // Customer retrieves the final order details.
        using var customerOrderRequest = CreateRequest(
            HttpMethod.Get,
            $"/api/orders/my/{order.OrderId:D}",
            customer.AccessToken);

        using var customerOrderResponse =
            await _client.SendAsync(customerOrderRequest);

        Assert.That(
            customerOrderResponse.StatusCode,
            Is.EqualTo(HttpStatusCode.OK),
            await customerOrderResponse.Content.ReadAsStringAsync());

        var orderDetails =
            await customerOrderResponse.Content
                .ReadFromJsonAsync<GetMyOrderByIdResponse>()
            ?? throw new AssertionException(
                "Order details endpoint returned an empty response.");

        Assert.That(orderDetails.Status, Is.EqualTo(OrderStatus.Delivered));
        Assert.That(orderDetails.Groups, Has.Count.EqualTo(1));

        Assert.That(
            orderDetails.Groups[0].Status,
            Is.EqualTo(OrderGroupStatus.Delivered));

        Assert.That(
            orderDetails.Groups[0].ShippingCarrier,
            Is.EqualTo("Test Carrier"));

        Assert.That(
            orderDetails.Groups[0].TrackingCode,
            Is.EqualTo("E2E-TRACK-0001"));

        // Verify the final persisted state.
        var finalState = await ReadOrderStateAsync(order.OrderId);

        Assert.That(finalState.Status, Is.EqualTo(OrderStatus.Delivered));
        Assert.That(finalState.PaymentStatus, Is.EqualTo(PaymentStatus.Successful));
    }

    [Test]
    public async Task AuthorizationMatrix_EnforcesAnonymousCustomerSellerAndAdminBoundaries()
    {
        var customer = await RegisterAndLoginAsync("authorization-customer");
        var seller = await LoginAsync(SeededSellerEmail, SeededPassword);
        var admin = await LoginAsync(AdminEmail, SeededPassword);

        await AssertStatusAsync(HttpMethod.Get, "/api/products", null, HttpStatusCode.OK);
        await AssertStatusAsync(HttpMethod.Get, "/api/cart", null, HttpStatusCode.Unauthorized);
        await AssertStatusAsync(HttpMethod.Get, "/api/admin/orders", null, HttpStatusCode.Unauthorized);
        await AssertStatusAsync(HttpMethod.Get, "/api/seller-orders", null, HttpStatusCode.Unauthorized);

        await AssertStatusAsync(HttpMethod.Get, "/api/orders/my", customer.AccessToken, HttpStatusCode.OK);
        await AssertStatusAsync(HttpMethod.Get, "/api/admin/orders", customer.AccessToken, HttpStatusCode.Forbidden);
        await AssertStatusAsync(HttpMethod.Get, "/api/seller-orders", customer.AccessToken, HttpStatusCode.Forbidden);
        await AssertStatusAsync(HttpMethod.Get, "/api/seller-offers/my", customer.AccessToken, HttpStatusCode.Forbidden);
        await AssertStatusAsync(HttpMethod.Post, "/api/products", customer.AccessToken, HttpStatusCode.Forbidden);

        await AssertStatusAsync(HttpMethod.Get, "/api/seller-orders", seller.AccessToken, HttpStatusCode.OK);
        await AssertStatusAsync(HttpMethod.Get, "/api/seller-offers/my", seller.AccessToken, HttpStatusCode.OK);
        await AssertStatusAsync(HttpMethod.Get, "/api/admin/orders", seller.AccessToken, HttpStatusCode.Forbidden);

        await AssertStatusAsync(HttpMethod.Get, "/api/admin/orders", admin.AccessToken, HttpStatusCode.OK);
        await AssertStatusAsync(HttpMethod.Get, "/api/seller-orders", admin.AccessToken, HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task RefreshCookie_RotatesDetectsReplayAndLogoutRevokesTheTokenFamily()
    {
        var email = NewEmail("refresh");
        await RegisterAsync(email);

        using (var loginWithoutOrigin = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email, password = CustomerPassword })
        })
        using (var rejectedLogin = await _client.SendAsync(loginWithoutOrigin))
            Assert.That(rejectedLogin.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        var initialSession = await LoginAsync(email, CustomerPassword);
        Assert.That(initialSession.RefreshCookie, Does.StartWith("ecommerce.refresh-token="));
        Assert.That(initialSession.SetCookieHeader, Does.Contain("httponly").IgnoreCase);
        Assert.That(initialSession.SetCookieHeader, Does.Contain("samesite=strict").IgnoreCase);
        Assert.That(initialSession.SetCookieHeader, Does.Contain("path=/api/auth").IgnoreCase);

        using var rotationResponse = await SendCookiePostAsync("/api/auth/refresh", initialSession.RefreshCookie);
        Assert.That(rotationResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var rotatedCookie = ReadRefreshCookie(rotationResponse);
        Assert.That(rotatedCookie, Is.Not.EqualTo(initialSession.RefreshCookie));

        using (var refreshWithoutOrigin = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh"))
        {
            refreshWithoutOrigin.Headers.TryAddWithoutValidation("Cookie", initialSession.RefreshCookie);
            using var rejectedRefresh = await _client.SendAsync(refreshWithoutOrigin);
            Assert.That(rejectedRefresh.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        // This assertion verifies confirmed replay, not a second tab racing the rotation.
        // Wait beyond the test host's one-second grace window before reusing the old cookie.
        await Task.Delay(TimeSpan.FromMilliseconds(1_500));

        using var replayResponse = await SendCookiePostAsync("/api/auth/refresh", initialSession.RefreshCookie);
        Assert.That(replayResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        // Replaying a revoked token revokes the whole family, including the replacement.
        using var familyRevokedResponse = await SendCookiePostAsync("/api/auth/refresh", rotatedCookie);
        Assert.That(familyRevokedResponse.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));

        var logoutSession = await LoginAsync(email, CustomerPassword);
        using (var logoutWithoutOrigin = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout"))
        {
            logoutWithoutOrigin.Headers.TryAddWithoutValidation("Cookie", logoutSession.RefreshCookie);
            using var rejectedLogout = await _client.SendAsync(logoutWithoutOrigin);
            Assert.That(rejectedLogout.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        }

        using var logoutResponse = await SendCookiePostAsync("/api/auth/logout", logoutSession.RefreshCookie);
        Assert.That(logoutResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        using var refreshAfterLogout = await SendCookiePostAsync("/api/auth/refresh", logoutSession.RefreshCookie);
        Assert.That(refreshAfterLogout.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task ZarinPalCallback_HandlesNokAndIndeterminateResultsWithoutReleasingReservedStock()
    {
        var customerNok = await RegisterAndLoginAsync("callback-nok");
        var nokOrder = await CreateOrderForOfferAsync(customerNok.AccessToken, _seededSellerOfferId);
        var nokPayment = await InitiatePaymentAsync(customerNok.AccessToken, nokOrder.OrderId);
        var nokStockAfterReservation = await ReadOfferStockAsync(_seededSellerOfferId);
        var verificationCountBeforeNok = _paymentGateway.VerificationCount;

        using (var nokCallback = await _client.GetAsync(BuildCallbackUrl(
                   nokPayment.PaymentId,
                   _paymentGateway.GetAuthority(nokPayment.PaymentId),
                   "NOK")))
        {
            Assert.That(nokCallback.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(nokCallback.Headers.Location!.ToString(), Does.Contain("status=failed"));
        }

        Assert.That(_paymentGateway.VerificationCount, Is.EqualTo(verificationCountBeforeNok));
        var nokState = await ReadOrderStateAsync(nokOrder.OrderId);
        Assert.That(nokState.PaymentStatus, Is.EqualTo(PaymentStatus.Failed));
        Assert.That(nokState.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
        Assert.That(await ReadOfferStockAsync(_seededSellerOfferId), Is.EqualTo(nokStockAfterReservation));

        var customerUnknown = await RegisterAndLoginAsync("callback-indeterminate");
        var unknownOrder = await CreateOrderForOfferAsync(customerUnknown.AccessToken, _seededSellerOfferId);
        var unknownPayment = await InitiatePaymentAsync(customerUnknown.AccessToken, unknownOrder.OrderId);
        var unknownStockAfterReservation = await ReadOfferStockAsync(_seededSellerOfferId);

        _paymentGateway.EnqueueVerification(new PaymentGatewayVerificationResult(
            IsSuccessful: false,
            IsAlreadyVerified: false,
            RefId: null,
            ErrorCode: "-50",
            ErrorMessage: "Provider could not determine the transaction status.",
            IsIndeterminate: true));

        using (var unknownCallback = await _client.GetAsync(BuildCallbackUrl(
                   unknownPayment.PaymentId,
                   _paymentGateway.GetAuthority(unknownPayment.PaymentId),
                   "OK")))
        {
            Assert.That(unknownCallback.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(unknownCallback.Headers.Location!.ToString(), Does.Contain("status=pending"));
        }

        var unknownState = await ReadOrderStateAsync(unknownOrder.OrderId);
        Assert.That(unknownState.PaymentStatus, Is.EqualTo(PaymentStatus.Pending));
        Assert.That(unknownState.VerificationAttemptCount, Is.EqualTo(1));
        Assert.That(unknownState.Status, Is.EqualTo(OrderStatus.AwaitingPayment));
        Assert.That(await ReadOfferStockAsync(_seededSellerOfferId), Is.EqualTo(unknownStockAfterReservation));

        var verificationCountAfterIndeterminate = _paymentGateway.VerificationCount;
        using (var lateNokCallback = await _client.GetAsync(BuildCallbackUrl(
                   unknownPayment.PaymentId,
                   _paymentGateway.GetAuthority(unknownPayment.PaymentId),
                   "NOK")))
        {
            Assert.That(lateNokCallback.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
            Assert.That(lateNokCallback.Headers.Location!.ToString(), Does.Contain("status=pending"));
        }

        Assert.That(_paymentGateway.VerificationCount, Is.EqualTo(verificationCountAfterIndeterminate));
        var stateAfterLateNok = await ReadOrderStateAsync(unknownOrder.OrderId);
        Assert.That(stateAfterLateNok.PaymentStatus, Is.EqualTo(PaymentStatus.Pending));
        Assert.That(stateAfterLateNok.VerificationAttemptCount, Is.EqualTo(1));
        Assert.That(await ReadOfferStockAsync(_seededSellerOfferId), Is.EqualTo(unknownStockAfterReservation));
    }

    [Test]
    public async Task OrderExpirationBackgroundService_ExpiresAnOldOrderThroughItsRealScopedDependencies()
    {
        var customer = await RegisterAndLoginAsync("expiration-worker");
        var order = await CreateOrderForOfferAsync(customer.AccessToken, _seededSellerOfferId);
        var stockAfterReservation = await ReadOfferStockAsync(_seededSellerOfferId);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();
            var expiredCreatedAt = DateTime.UtcNow.AddHours(-1);
            await dbContext.Orders
                .Where(candidate => candidate.Id == order.OrderId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    candidate => candidate.CreatedAt,
                    expiredCreatedAt));
        }

        var expirationService = new OrderExpirationBackgroundService(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            _factory.Services.GetRequiredService<IOptions<ECommerce.Application.Options.OrderExpirationOptions>>(),
            _factory.Services.GetRequiredService<ILogger<OrderExpirationBackgroundService>>());

        await expirationService.StartAsync(CancellationToken.None);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            OrderStatus? observedStatus = null;

            while (DateTime.UtcNow < deadline)
            {
                await using var scope = _factory.Services.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();
                observedStatus = await dbContext.Orders
                    .AsNoTracking()
                    .Where(candidate => candidate.Id == order.OrderId)
                    .Select(candidate => (OrderStatus?)candidate.Status)
                    .SingleOrDefaultAsync();

                if (observedStatus == OrderStatus.Cancelled)
                    break;

                await Task.Delay(100);
            }

            Assert.That(observedStatus, Is.EqualTo(OrderStatus.Cancelled));
            Assert.That(await ReadOfferStockAsync(_seededSellerOfferId), Is.EqualTo(stockAfterReservation + 1));
        }
        finally
        {
            await expirationService.StopAsync(CancellationToken.None);
            expirationService.Dispose();
        }
    }

    private async Task<AuthSession> RegisterAndLoginAsync(string prefix)
    {
        var email = NewEmail(prefix);
        await RegisterAsync(email);
        return await LoginAsync(email, CustomerPassword);
    }

    private async Task RegisterAsync(string email)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new
            {
                email,
                password = CustomerPassword,
                firstName = "API",
                lastName = "Test"
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            await response.Content.ReadAsStringAsync());
    }

    private async Task<AuthSession> LoginAsync(string email, string password)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email, password })
        };
        request.Headers.TryAddWithoutValidation("Origin", TestOrigin);

        using var response = await _client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            await response.Content.ReadAsStringAsync());

        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        var accessToken = json.RootElement.GetProperty("accessToken").GetString();
        Assert.That(accessToken, Is.Not.Null.And.Not.Empty);

        var setCookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("ecommerce.refresh-token=", StringComparison.OrdinalIgnoreCase));

        return new AuthSession(email, accessToken!, ExtractCookiePair(setCookie), setCookie);
    }

    private async Task<Guid> CreateSellerOfferAsync(
        string sellerAccessToken,
        Guid productVariantId,
        int stock)
    {
        using var response = await SendJsonAsync(
            HttpMethod.Post,
            "/api/seller-offers",
            sellerAccessToken,
            new { productVariantId, price = 499000m, stock });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            await response.Content.ReadAsStringAsync());
        var sellerOfferId = await ReadIdAsync(response);

        uint rowVersion;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();
            rowVersion = await dbContext.SellerOffers
                .Where(offer => offer.Id == sellerOfferId)
                .Select(offer => offer.RowVersion)
                .SingleAsync();
        }

        using var activateResponse = await SendJsonAsync(
            HttpMethod.Put,
            "/api/seller-offers/activate",
            sellerAccessToken,
            new { sellerOfferId, rowVersion });
        Assert.That(activateResponse.StatusCode, Is.EqualTo(HttpStatusCode.NoContent),
            await activateResponse.Content.ReadAsStringAsync());

        return sellerOfferId;
    }

    private async Task AddToCartAsync(string customerAccessToken, Guid sellerOfferId, int quantity)
    {
        using var response = await SendJsonAsync(
            HttpMethod.Post,
            "/api/cart/items",
            customerAccessToken,
            new { sellerOfferId, quantity });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            await response.Content.ReadAsStringAsync());
    }

    private async Task<CreateOrderFromCartResponse> CreateOrderAsync(string customerAccessToken)
    {
        using var response = await SendJsonAsync(
            HttpMethod.Post,
            "/api/orders/from-cart",
            customerAccessToken,
            new
            {
                shippingRecipientName = "API Test Customer",
                shippingPhoneNumber = "09120000000",
                shippingProvince = "Tehran",
                shippingCity = "Tehran",
                shippingPostalCode = "1234567890",
                shippingAddressLine = "Integration test address, Unit 1"
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<CreateOrderFromCartResponse>()
               ?? throw new AssertionException("Create order returned an empty response.");
    }

    private async Task<CreateOrderFromCartResponse> CreateOrderForOfferAsync(
        string customerAccessToken,
        Guid sellerOfferId)
    {
        await AddToCartAsync(customerAccessToken, sellerOfferId, quantity: 1);
        return await CreateOrderAsync(customerAccessToken);
    }

    private async Task<InitiatePaymentResponse> InitiatePaymentAsync(
        string customerAccessToken,
        Guid orderId)
    {
        using var request = CreateRequest(
            HttpMethod.Post,
            $"/api/payments/orders/{orderId:D}/initiate",
            customerAccessToken);
        using var response = await _client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<InitiatePaymentResponse>()
               ?? throw new AssertionException("Payment initiation returned an empty response.");
    }

    private async Task UpdateSellerOrderStatusAsync(
        string sellerAccessToken,
        Guid orderId,
        OrderGroupStatus newStatus,
        string? shippingCarrier = null,
        string? trackingCode = null)
    {
        using var response = await SendJsonAsync(
            HttpMethod.Patch,
            $"/api/seller-orders/{orderId:D}/status",
            sellerAccessToken,
            new
            {
                newStatus = (int)newStatus,
                shippingCarrier,
                trackingCode
            });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            await response.Content.ReadAsStringAsync());
    }

    private async Task AssertStatusAsync(
        HttpMethod method,
        string path,
        string? accessToken,
        HttpStatusCode expectedStatus)
    {
        using var request = CreateRequest(method, path, accessToken);
        using var response = await _client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(expectedStatus),
            $"{method} {path}: {await response.Content.ReadAsStringAsync()}");
    }

    private async Task<HttpResponseMessage> SendJsonAsync(
        HttpMethod method,
        string path,
        string? accessToken,
        object body)
    {
        using var request = CreateRequest(method, path, accessToken);
        request.Content = JsonContent.Create(body);
        return await _client.SendAsync(request);
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        string? accessToken)
    {
        var request = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private async Task<HttpResponseMessage> SendCookiePostAsync(string path, string cookiePair)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.TryAddWithoutValidation("Origin", TestOrigin);
        request.Headers.TryAddWithoutValidation("Cookie", cookiePair);
        return await _client.SendAsync(request);
    }

    private static string BuildCallbackUrl(Guid paymentId, string authority, string status)
        => "/api/payments/zarinpal/callback" +
           $"?paymentId={paymentId:D}&Authority={Uri.EscapeDataString(authority)}&Status={Uri.EscapeDataString(status)}";

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return json.RootElement.GetProperty("id").GetGuid();
    }

    private static string ReadRefreshCookie(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie")
            .Single(value => value.StartsWith("ecommerce.refresh-token=", StringComparison.OrdinalIgnoreCase));
        return ExtractCookiePair(setCookie);
    }

    private static string ExtractCookiePair(string setCookie)
        => setCookie.Split(';', 2)[0];

    private async Task<(OrderStatus Status, PaymentStatus? PaymentStatus, int VerificationAttemptCount)> ReadOrderStateAsync(
        Guid orderId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();
        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(candidate => candidate.Payments)
            .SingleAsync(candidate => candidate.Id == orderId);
        var payment = order.Payments.SingleOrDefault();

        return (order.Status, payment?.Status, payment?.VerificationAttemptCount ?? 0);
    }

    private async Task<int> ReadOfferStockAsync(Guid sellerOfferId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ECommerceDbContext>();
        return await dbContext.SellerOffers
            .AsNoTracking()
            .Where(offer => offer.Id == sellerOfferId)
            .Select(offer => offer.Stock)
            .SingleAsync();
    }

    private static string NewEmail(string prefix)
        => $"{prefix}-{Guid.NewGuid():N}@integration.test";

    private sealed record AuthSession(
        string Email,
        string AccessToken,
        string RefreshCookie,
        string SetCookieHeader);
}
