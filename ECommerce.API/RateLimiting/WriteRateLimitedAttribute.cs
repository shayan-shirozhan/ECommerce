namespace ECommerce.API.RateLimiting;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class WriteRateLimitedAttribute : Attribute
{
}
