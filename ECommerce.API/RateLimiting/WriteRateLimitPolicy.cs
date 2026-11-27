using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace ECommerce.API.RateLimiting;

internal static class WriteRateLimitPolicy
{
    public static PartitionedRateLimiter<HttpContext> CreateGlobalLimiter(
        bool isEnabled,
        int permitLimit,
        TimeSpan window)
    {
        return PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            var isWriteEndpoint = httpContext.GetEndpoint()?
                .Metadata.GetMetadata<WriteRateLimitedAttribute>() is not null;

            if (!isEnabled || !isWriteEndpoint)
                return RateLimitPartition.GetNoLimiter("write-policy-disabled");

            var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });
    }
}
