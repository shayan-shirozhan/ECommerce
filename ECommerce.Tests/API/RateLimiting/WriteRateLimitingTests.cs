using System.Net;
using System.Reflection;
using ECommerce.API.Controllers;
using ECommerce.API.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.API.RateLimiting;

public sealed class WriteRateLimitingTests
{
    [Test]
    public async Task Enabled_write_policy_should_limit_each_ip_independently()
    {
        await using var app = await CreateTestApplicationAsync(isEnabled: true, permitLimit: 2);
        using var client = app.GetTestClient();

        Assert.That(await SendWriteAsync(client, "/write/cart", "198.51.100.10"), Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await SendWriteAsync(client, "/write/order", "198.51.100.10"), Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await SendWriteAsync(client, "/write/payment", "198.51.100.10"),
            Is.EqualTo(HttpStatusCode.TooManyRequests));

        // A different client IP receives an independent quota.
        Assert.That(await SendWriteAsync(client, "/write/cart", "198.51.100.11"), Is.EqualTo(HttpStatusCode.OK));
        // Read-only endpoints are not part of the global write quota.
        using (var readResponse = await client.GetAsync("/read"))
            Assert.That(readResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Write_policy_should_be_disabled_by_default_outside_demo()
    {
        var configuration = new ConfigurationBuilder().Build();
        var isEnabled = WriteRateLimitPolicy.ResolveEnabled(configuration, isDemo: false);
        await using var app = await CreateTestApplicationAsync(isEnabled, permitLimit: 1);
        using var client = app.GetTestClient();

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 4; attempt++)
            statuses.Add(await SendWriteAsync(client, "/write/cart", "198.51.100.20"));

        Assert.That(statuses, Is.All.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Production_write_policy_should_be_enabled_by_configuration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Write:Enabled"] = "true"
            })
            .Build();
        var isEnabled = WriteRateLimitPolicy.ResolveEnabled(configuration, isDemo: false);

        await using var app = await CreateTestApplicationAsync(isEnabled, permitLimit: 1);
        using var client = app.GetTestClient();

        Assert.That(await SendWriteAsync(client, "/write/cart", "198.51.100.30"), Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await SendWriteAsync(client, "/write/order", "198.51.100.30"),
            Is.EqualTo(HttpStatusCode.TooManyRequests));
    }

    [Test]
    public void Write_policy_should_default_to_demo_and_allow_explicit_overrides()
    {
        var defaultConfiguration = new ConfigurationBuilder().Build();
        Assert.That(WriteRateLimitPolicy.ResolveEnabled(defaultConfiguration, isDemo: true), Is.True);
        Assert.That(WriteRateLimitPolicy.ResolveEnabled(defaultConfiguration, isDemo: false), Is.False);

        var disabledConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Write:Enabled"] = "false"
            })
            .Build();
        var enabledConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Write:Enabled"] = "true"
            })
            .Build();

        Assert.That(WriteRateLimitPolicy.ResolveEnabled(disabledConfiguration, isDemo: true), Is.False);
        Assert.That(WriteRateLimitPolicy.ResolveEnabled(enabledConfiguration, isDemo: false), Is.True);
    }

    [TestCase(typeof(CartController), nameof(CartController.AddItem))]
    [TestCase(typeof(CartController), nameof(CartController.UpdateItem))]
    [TestCase(typeof(CartController), nameof(CartController.RemoveItem))]
    [TestCase(typeof(OrdersController), nameof(OrdersController.CreateFromCart))]
    [TestCase(typeof(OrdersController), nameof(OrdersController.CancelMyOrder))]
    [TestCase(typeof(PaymentsController), nameof(PaymentsController.InitiatePayment))]
    [TestCase(typeof(PaymentsController), nameof(PaymentsController.ZarinPalCallback))]
    [TestCase(typeof(AuthController), nameof(AuthController.Register))]
    [TestCase(typeof(WishlistController), nameof(WishlistController.AddItem))]
    [TestCase(typeof(WishlistController), nameof(WishlistController.RemoveItem))]
    [TestCase(typeof(AddressesController), nameof(AddressesController.Create))]
    [TestCase(typeof(AddressesController), nameof(AddressesController.Update))]
    [TestCase(typeof(AddressesController), nameof(AddressesController.SetDefault))]
    [TestCase(typeof(AddressesController), nameof(AddressesController.Delete))]
    [TestCase(typeof(SellerRequestsController), nameof(SellerRequestsController.Create))]
    [TestCase(typeof(SellerRequestsController), nameof(SellerRequestsController.Approve))]
    [TestCase(typeof(SellerRequestsController), nameof(SellerRequestsController.Reject))]
    [TestCase(typeof(UsersController), nameof(UsersController.UpdateMyProfile))]
    public void Configured_write_actions_should_use_the_write_policy(Type controllerType, string actionName)
    {
        var action = controllerType.GetMethod(actionName, BindingFlags.Instance | BindingFlags.Public);
        Assert.That(action, Is.Not.Null, $"Could not find {controllerType.Name}.{actionName}.");

        Assert.That(action!.GetCustomAttribute<WriteRateLimitedAttribute>(), Is.Not.Null);
    }

    [Test]
    public void Registration_should_remain_covered_by_the_existing_auth_policy()
    {
        var attribute = typeof(AuthController).GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.That(attribute?.PolicyName, Is.EqualTo("auth"));
    }

    private static async Task<WebApplication> CreateTestApplicationAsync(bool isEnabled, int permitLimit)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = WriteRateLimitPolicy.CreateGlobalLimiter(
                isEnabled: isEnabled,
                permitLimit: permitLimit,
                window: TimeSpan.FromMinutes(1));
        });

        var app = builder.Build();

        // Test-only injection of the remote address. Production derives it exclusively
        // from the connection populated by trusted forwarded-header middleware.
        app.Use(async (context, next) =>
        {
            if (IPAddress.TryParse(context.Request.Headers["X-Test-Remote-IP"].ToString(), out var remoteIp))
                context.Connection.RemoteIpAddress = remoteIp;

            await next();
        });

        app.UseRouting();
        app.UseRateLimiter();
        app.MapPost("/write/cart", () => Results.Ok())
            .WithMetadata(new WriteRateLimitedAttribute());
        app.MapPost("/write/order", () => Results.Ok())
            .WithMetadata(new WriteRateLimitedAttribute());
        app.MapPost("/write/payment", () => Results.Ok())
            .WithMetadata(new WriteRateLimitedAttribute());
        app.MapGet("/read", () => Results.Ok());

        await app.StartAsync();
        return app;
    }

    private static async Task<HttpStatusCode> SendWriteAsync(
        HttpClient client,
        string path,
        string remoteIp)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("X-Test-Remote-IP", remoteIp);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }
}
