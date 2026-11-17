using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;

namespace ECommerce.API.Middleware;

/// <summary>
/// Protects cookie-setting and cookie-authenticated endpoints from cross-site request forgery.
/// SameSite cookies and CORS are useful defense-in-depth, but do not replace source-origin validation.
/// </summary>
public sealed class CookieRequestOriginMiddleware(
    RequestDelegate next,
    IConfiguration configuration)
{
    private readonly HashSet<string> _allowedOrigins = LoadAllowedOrigins(configuration);

    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsProtectedRequest(context.Request))
        {
            await next(context);
            return;
        }

        var sourceOrigin = GetSourceOrigin(context.Request);
        var requestOrigin = TryNormalizeOrigin(
            $"{context.Request.Scheme}://{context.Request.Host}",
            allowPath: false);

        var isAllowed = sourceOrigin is not null &&
            (string.Equals(sourceOrigin, requestOrigin, StringComparison.OrdinalIgnoreCase) ||
             _allowedOrigins.Contains(sourceOrigin));

        if (!isAllowed)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsJsonAsync(
                new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Invalid request origin.",
                    Detail = "The Origin or Referer header is missing or is not trusted for this endpoint.",
                    Instance = context.Request.Path
                },
                context.RequestAborted);

            return;
        }

        await next(context);
    }

    private static bool IsProtectedRequest(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method))
            return false;

        var path = request.Path.Value?.TrimEnd('/');
        return string.Equals(path, "/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(path, "/api/auth/refresh", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(path, "/api/auth/logout", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetSourceOrigin(HttpRequest request)
    {
        // If Origin is present, it takes precedence. In particular, Origin: null or
        // a malformed Origin must not be rescued by a Referer header.
        if (request.Headers.TryGetValue(HeaderNames.Origin, out var originHeader))
            return TryNormalizeOrigin(originHeader.ToString(), allowPath: false);

        if (request.Headers.TryGetValue(HeaderNames.Referer, out var refererHeader))
            return TryNormalizeOrigin(refererHeader.ToString(), allowPath: true);

        return null;
    }

    private static HashSet<string> LoadAllowedOrigins(IConfiguration configuration)
    {
        var configuredOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        var allowedOrigins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var configuredOrigin in configuredOrigins)
        {
            var normalizedOrigin = TryNormalizeOrigin(configuredOrigin, allowPath: false);
            if (normalizedOrigin is null)
            {
                throw new InvalidOperationException(
                    $"Cors:AllowedOrigins contains an invalid origin: '{configuredOrigin}'. Configure origins as scheme://host[:port] without a path.");
            }

            allowedOrigins.Add(normalizedOrigin);
        }

        return allowedOrigins;
    }

    private static string? TryNormalizeOrigin(string value, bool allowPath)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) ||
            !string.IsNullOrEmpty(uri.UserInfo))
        {
            return null;
        }

        if (!allowPath &&
            (uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)))
        {
            return null;
        }

        return uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();
    }
}
