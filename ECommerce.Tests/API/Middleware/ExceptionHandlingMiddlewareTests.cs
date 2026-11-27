using ECommerce.API.Middleware;
using ECommerce.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ECommerce.Tests.API.Middleware;

public sealed class ExceptionHandlingMiddlewareTests
{
    [Test]
    public async Task Refresh_rotation_in_progress_should_return_conflict_and_retry_after_header()
    {
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new RefreshTokenRotationInProgressException(),
            NullLogger<ExceptionHandlingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddOptions();
        using var serviceProvider = services.BuildServiceProvider();
        context.RequestServices = serviceProvider;
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
            Assert.That(context.Response.Headers["Retry-After"].ToString(), Is.EqualTo("1"));
            Assert.That(context.Response.ContentType, Is.EqualTo("application/problem+json"));
        });
    }
}
