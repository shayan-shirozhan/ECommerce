using ECommerce.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;

namespace ECommerce.Tests.API.Middleware;

public sealed class CookieRequestOriginMiddlewareTests
{
    [Test]
    public async Task Should_allow_a_same_origin_refresh_request()
    {
        var (context, nextCalled) = await InvokeAsync(
            path: "/api/auth/refresh",
            origin: "https://api.example.com");

        Assert.Multiple(() =>
        {
            Assert.That(nextCalled, Is.True);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
        });
    }

    [Test]
    public async Task Should_allow_a_configured_frontend_origin()
    {
        var (context, nextCalled) = await InvokeAsync(
            path: "/api/auth/refresh",
            origin: "https://shop.example.com",
            allowedOrigins: ["https://shop.example.com"]);

        Assert.Multiple(() =>
        {
            Assert.That(nextCalled, Is.True);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
        });
    }

    [Test]
    public async Task Should_reject_an_untrusted_sibling_subdomain()
    {
        var (context, nextCalled) = await InvokeAsync(
            path: "/api/auth/refresh",
            origin: "https://evil.example.com",
            allowedOrigins: ["https://shop.example.com"]);

        Assert.Multiple(() =>
        {
            Assert.That(nextCalled, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        });
    }

    [Test]
    public async Task Should_reject_a_request_without_origin_or_referer()
    {
        var (context, nextCalled) = await InvokeAsync(
            path: "/api/auth/logout",
            origin: null);

        Assert.Multiple(() =>
        {
            Assert.That(nextCalled, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        });
    }

    [Test]
    public async Task Should_accept_a_trusted_referer_when_origin_is_absent()
    {
        var (context, nextCalled) = await InvokeAsync(
            path: "/api/auth/logout",
            origin: null,
            referer: "https://shop.example.com/account/security",
            allowedOrigins: ["https://shop.example.com"]);

        Assert.Multiple(() =>
        {
            Assert.That(nextCalled, Is.True);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
        });
    }

    [Test]
    public async Task Should_reject_null_origin_even_when_referer_is_trusted()
    {
        var (context, nextCalled) = await InvokeAsync(
            path: "/api/auth/refresh",
            origin: "null",
            referer: "https://shop.example.com/account",
            allowedOrigins: ["https://shop.example.com"]);

        Assert.Multiple(() =>
        {
            Assert.That(nextCalled, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        });
    }

    [Test]
    public async Task Should_allow_a_same_origin_login_request()
    {
        var (context, nextCalled) = await InvokeAsync(
            path: "/api/auth/login",
            origin: "https://api.example.com");

        Assert.Multiple(() =>
        {
            Assert.That(nextCalled, Is.True);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
        });
    }

    [Test]
    public async Task Should_reject_an_untrusted_login_request()
    {
        var (context, nextCalled) = await InvokeAsync(
            path: "/api/auth/login",
            origin: "https://evil.example.com",
            allowedOrigins: ["https://shop.example.com"]);

        Assert.Multiple(() =>
        {
            Assert.That(nextCalled, Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
        });
    }

    [Test]
    public async Task Should_not_apply_origin_validation_to_unprotected_endpoints()
    {
        var (context, nextCalled) = await InvokeAsync(
            path: "/api/auth/register",
            origin: null);

        Assert.Multiple(() =>
        {
            Assert.That(nextCalled, Is.True);
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
        });
    }

    private static async Task<(DefaultHttpContext Context, bool NextCalled)> InvokeAsync(
        string path,
        string? origin,
        string? referer = null,
        string[]? allowedOrigins = null)
    {
        var configurationValues = new Dictionary<string, string?>();
        for (var index = 0; index < (allowedOrigins?.Length ?? 0); index++)
            configurationValues[$"Cors:AllowedOrigins:{index}"] = allowedOrigins![index];

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationValues)
            .Build();

        var nextCalled = false;
        var middleware = new CookieRequestOriginMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            configuration);

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = path;
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("api.example.com");

        if (origin is not null)
            context.Request.Headers[HeaderNames.Origin] = origin;

        if (referer is not null)
            context.Request.Headers[HeaderNames.Referer] = referer;

        await middleware.InvokeAsync(context);
        return (context, nextCalled);
    }
}
