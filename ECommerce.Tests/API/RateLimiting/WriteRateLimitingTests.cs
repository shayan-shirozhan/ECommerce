using System.Net;
using System.Reflection;
using ECommerce.API.Controllers;
using ECommerce.API.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.API.RateLimiting;

public sealed class WriteRateLimitingTests
{
    [Test]
    public async Task Demo_write_policy_should_limit_each_ip_independently()
    {
        await using var app = await CreateTestApplicationAsync(isDemo: true, permitLimit: 2);
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
    public async Task Write_policy_should_be_disabled_outside_demo()
    {
        await using var app = await CreateTestApplicationAsync(isDemo: false, permitLimit: 1);
        using var client = app.GetTestClient();

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 4; attempt++)
            statuses.Add(await SendWriteAsync(client, "/write/cart", "198.51.100.20"));

        Assert.That(statuses, Is.All.EqualTo(HttpStatusCode.OK));
    }

    [TestCase(typeof(CartController), nameof(CartController.AddItem))]
    [TestCase(typeof(CartController), nameof(CartController.UpdateItem))]
    [TestCase(typeof(CartController), nameof(CartController.RemoveItem))]
    [TestCase(typeof(OrdersController), nameof(OrdersController.CreateFromCart))]
    [TestCase(typeof(OrdersController), nameof(OrdersController.CancelMyOrder))]
    [TestCase(typeof(PaymentsController), nameof(PaymentsController.InitiatePayment))]
    [TestCase(typeof(PaymentsController), nameof(PaymentsController.ZarinPalCallback))]
    [TestCase(typeof(AuthController), nameof(AuthController.Register))]
    public void Visitor_write_actions_should_use_the_write_policy(Type controllerType, string actionName)
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

    private static async Task<WebApplication> CreateTestApplicationAsync(bool isDemo, int permitLimit)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = WriteRateLimitPolicy.CreateGlobalLimiter(
                isEnabled: isDemo,
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
