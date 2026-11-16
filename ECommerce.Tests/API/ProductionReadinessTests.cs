using System.Reflection;
using ECommerce.API.Controllers;
using ECommerce.API.HealthChecks;
using ECommerce.Infrastructure.Persistence;
using ECommerce.Tests.Infrastructure.Fixtures;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ECommerce.Tests.API;

public sealed class ProductionReadinessTests : PostgresTestBase
{
    [Test]
    public async Task Database_health_check_should_report_healthy_when_postgres_is_reachable()
    {
        await using var dbContext = CreateDbContext();
        var healthCheck = new DatabaseHealthCheck(dbContext);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.That(result.Status, Is.EqualTo(HealthStatus.Healthy));
    }

    [Test]
    public async Task Database_health_check_should_report_unhealthy_when_postgres_is_unreachable()
    {
        var options = new DbContextOptionsBuilder<ECommerceDbContext>()
            .UseNpgsql(
                "Host=127.0.0.1;Port=1;Database=ecommerce_unavailable;Username=postgres;Password=postgres;Timeout=1;Command Timeout=1")
            .Options;

        await using var dbContext = new ECommerceDbContext(options);
        var healthCheck = new DatabaseHealthCheck(dbContext);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.That(result.Status, Is.EqualTo(HealthStatus.Unhealthy));
    }

    [Test]
    public void Auth_controller_should_apply_the_named_rate_limiting_policy()
    {
        var attribute = typeof(AuthController)
            .GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.That(attribute, Is.Not.Null);
        Assert.That(attribute!.PolicyName, Is.EqualTo("auth"));
    }
}
