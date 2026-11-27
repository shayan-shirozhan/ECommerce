using ECommerce.API.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace ECommerce.Tests.API.Middleware;

public sealed class SecurityHeadersMiddlewareTests
{
    [Test]
    public async Task Should_add_security_headers_to_the_response()
    {
        await using var app = await CreateTestApplicationAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/");

        Assert.Multiple(() =>
        {
            Assert.That(response.Headers.GetValues("X-Content-Type-Options").Single(), Is.EqualTo("nosniff"));
            Assert.That(response.Headers.GetValues("X-Frame-Options").Single(), Is.EqualTo("DENY"));
            Assert.That(
                response.Headers.GetValues("Referrer-Policy").Single(),
                Is.EqualTo("strict-origin-when-cross-origin"));
            Assert.That(
                response.Headers.GetValues("Permissions-Policy").Single(),
                Is.EqualTo("camera=(), microphone=(), geolocation=()"));
        });
    }

    [Test]
    public async Task Should_not_override_a_security_header_configured_by_the_host()
    {
        await using var app = await CreateTestApplicationAsync(
            headers => headers["X-Frame-Options"] = "SAMEORIGIN");
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/");

        Assert.That(response.Headers.GetValues("X-Frame-Options").Single(), Is.EqualTo("SAMEORIGIN"));
    }

    private static async Task<WebApplication> CreateTestApplicationAsync(
        Action<IHeaderDictionary>? configureHeaders = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();

        var app = builder.Build();

        if (configureHeaders is not null)
        {
            app.Use(async (context, next) =>
            {
                configureHeaders(context.Response.Headers);
                await next();
            });
        }

        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.Run(context => context.Response.WriteAsync("OK"));

        await app.StartAsync();
        return app;
    }
}
