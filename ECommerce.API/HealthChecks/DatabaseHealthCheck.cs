using ECommerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace ECommerce.API.HealthChecks;

public sealed class DatabaseHealthCheck(ECommerceDbContext dbContext) : IHealthCheck
{
    private const int ConnectionTimeoutSeconds = 3;
    private const int CommandTimeoutSeconds = 3;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var configuredConnectionString = dbContext.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(configuredConnectionString))
            {
                return HealthCheckResult.Unhealthy("The PostgreSQL connection string is not configured.");
            }

            // Do not reuse an idle pooled connector for readiness checks. A fresh TCP
            // connection ensures that an unavailable PostgreSQL instance is detected.
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(configuredConnectionString)
            {
                Pooling = false,
                Timeout = ConnectionTimeoutSeconds,
                CommandTimeout = CommandTimeoutSeconds
            };

            await using var connection = new NpgsqlConnection(connectionStringBuilder.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            command.CommandTimeout = CommandTimeoutSeconds;

            var result = await command.ExecuteScalarAsync(cancellationToken);

            return result is int value && value == 1
                ? HealthCheckResult.Healthy("PostgreSQL query succeeded.")
                : HealthCheckResult.Unhealthy("The PostgreSQL health query returned an unexpected result.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("The PostgreSQL health check failed.", exception);
        }
    }
}
