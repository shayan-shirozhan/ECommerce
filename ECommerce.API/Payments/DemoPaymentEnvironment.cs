using Microsoft.Extensions.Configuration;
using Npgsql;

namespace ECommerce.API.Payments;

/// <summary>
/// Validates the dedicated public-demo boundary before service registration.
/// The Demo environment must never fall back to the normal application database.
/// </summary>
internal static class DemoPaymentEnvironment
{
    private const string EnvironmentName = "Demo";
    private const string EnabledKey = "DemoPayments:Enabled";

    public static bool ValidateAndIsEnabled(
        string environmentName,
        IConfiguration configuration)
    {
        var isDemoEnvironment = string.Equals(
            environmentName,
            EnvironmentName,
            StringComparison.OrdinalIgnoreCase);
        var demoPaymentsEnabled = configuration.GetValue<bool>(EnabledKey);

        if (demoPaymentsEnabled && !isDemoEnvironment)
        {
            throw new InvalidOperationException(
                $"{EnabledKey} can only be enabled when ASPNETCORE_ENVIRONMENT={EnvironmentName}.");
        }

        if (isDemoEnvironment && !demoPaymentsEnabled)
        {
            throw new InvalidOperationException(
                $"{EnabledKey} must be explicitly set to true when ASPNETCORE_ENVIRONMENT={EnvironmentName}.");
        }

        if (!isDemoEnvironment)
            return false;

        if (configuration.GetValue<bool>("ZarinPal:Enabled"))
        {
            throw new InvalidOperationException(
                "ZarinPal must remain disabled in the Demo environment. Public Demo uses only its isolated mock payment gateway.");
        }

        var demoConnectionString = configuration.GetConnectionString("DemoECommerceDb");
        if (string.IsNullOrWhiteSpace(demoConnectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DemoECommerceDb is required in the Demo environment. The Demo environment never falls back to ConnectionStrings:ECommerceDb.");
        }

        var applicationConnectionString = configuration.GetConnectionString("ECommerceDb");
        if (!string.IsNullOrWhiteSpace(applicationConnectionString))
        {
            var demoDatabase = new NpgsqlConnectionStringBuilder(demoConnectionString);
            var applicationDatabase = new NpgsqlConnectionStringBuilder(applicationConnectionString);

            var targetsSameDatabase =
                string.Equals(demoDatabase.Host?.Trim(), applicationDatabase.Host?.Trim(), StringComparison.OrdinalIgnoreCase) &&
                demoDatabase.Port == applicationDatabase.Port &&
                string.Equals(demoDatabase.Database, applicationDatabase.Database, StringComparison.Ordinal);

            if (targetsSameDatabase)
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:DemoECommerceDb must target a different PostgreSQL host, port, or database from ConnectionStrings:ECommerceDb.");
            }
        }

        return true;
    }
}
