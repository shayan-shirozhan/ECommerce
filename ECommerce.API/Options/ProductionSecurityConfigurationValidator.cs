using Microsoft.Extensions.Configuration;

namespace ECommerce.API.Options;

internal static class ProductionSecurityConfigurationValidator
{
    public static void Validate(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var allowedHosts = configuration["AllowedHosts"]?
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

        if (allowedHosts.Length == 0 || allowedHosts.Any(host => host.Contains('*')))
        {
            throw new InvalidOperationException(
                "Production requires AllowedHosts to contain explicit host names; wildcard entries are not allowed.");
        }

        var dataProtectionKeysPath = configuration["DataProtection:KeysPath"];
        if (string.IsNullOrWhiteSpace(dataProtectionKeysPath))
        {
            throw new InvalidOperationException(
                "Production requires DataProtection:KeysPath to point to a durable, writable key-ring directory.");
        }

        if (!Path.IsPathFullyQualified(dataProtectionKeysPath))
        {
            throw new InvalidOperationException(
                "DataProtection:KeysPath must be an absolute path.");
        }
    }
}
