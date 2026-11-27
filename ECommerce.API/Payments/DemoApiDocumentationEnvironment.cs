using Microsoft.Extensions.Configuration;

namespace ECommerce.API.Payments;

/// <summary>
/// Controls optional API documentation exposure in the isolated public Demo environment.
/// Documentation can never be enabled in Development, Production, or another environment
/// through the Demo-specific setting.
/// </summary>
internal static class DemoApiDocumentationEnvironment
{
    private const string EnvironmentName = "Demo";
    private const string EnabledKey = "DemoApiDocumentation:Enabled";

    public static bool ValidateAndIsEnabled(
        string environmentName,
        IConfiguration configuration)
    {
        var isDemoEnvironment = string.Equals(
            environmentName,
            EnvironmentName,
            StringComparison.OrdinalIgnoreCase);
        var documentationEnabled = configuration.GetValue<bool>(EnabledKey);

        if (documentationEnabled && !isDemoEnvironment)
        {
            throw new InvalidOperationException(
                $"{EnabledKey} can only be enabled when ASPNETCORE_ENVIRONMENT={EnvironmentName}.");
        }

        return isDemoEnvironment && documentationEnabled;
    }
}
