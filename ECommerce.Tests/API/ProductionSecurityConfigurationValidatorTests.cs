using ECommerce.API.Options;
using Microsoft.Extensions.Configuration;

namespace ECommerce.Tests.API;

public sealed class ProductionSecurityConfigurationValidatorTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("*")]
    [TestCase("api.example.com;*")]
    public void Validate_should_reject_missing_or_wildcard_allowed_hosts(string? allowedHosts)
    {
        var configuration = CreateConfiguration(
            allowedHosts,
            Path.Combine(Path.GetTempPath(), "ecommerce-data-protection"));

        Assert.Throws<InvalidOperationException>(
            () => ProductionSecurityConfigurationValidator.Validate(configuration));
    }

    [Test]
    public void Validate_should_require_a_persistent_key_ring_path()
    {
        var configuration = CreateConfiguration("api.example.com", null);

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProductionSecurityConfigurationValidator.Validate(configuration));

        Assert.That(exception!.Message, Does.Contain("DataProtection:KeysPath"));
    }

    [Test]
    public void Validate_should_reject_a_relative_key_ring_path()
    {
        var configuration = CreateConfiguration("api.example.com", "./data-protection-keys");

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProductionSecurityConfigurationValidator.Validate(configuration));

        Assert.That(exception!.Message, Does.Contain("absolute path"));
    }

    [Test]
    public void Validate_should_accept_explicit_hosts_and_an_absolute_key_ring_path()
    {
        var configuration = CreateConfiguration(
            "api.example.com;shop.example.com",
            Path.Combine(Path.GetTempPath(), "ecommerce-data-protection"));

        Assert.DoesNotThrow(() => ProductionSecurityConfigurationValidator.Validate(configuration));
    }

    private static IConfiguration CreateConfiguration(string? allowedHosts, string? keysPath)
    {
        var values = new Dictionary<string, string?>();
        if (allowedHosts is not null)
            values["AllowedHosts"] = allowedHosts;
        if (keysPath is not null)
            values["DataProtection:KeysPath"] = keysPath;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
