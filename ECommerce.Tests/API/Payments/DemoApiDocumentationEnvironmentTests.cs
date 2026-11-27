using ECommerce.API.Payments;
using Microsoft.Extensions.Configuration;

namespace ECommerce.Tests.API.Payments;

public sealed class DemoApiDocumentationEnvironmentTests
{
    [TestCase("Development")]
    [TestCase("Production")]
    [TestCase("Staging")]
    public void ValidateAndIsEnabled_should_return_false_outside_demo_when_disabled(
        string environmentName)
    {
        var configuration = CreateConfiguration(enabled: false);

        var result = DemoApiDocumentationEnvironment.ValidateAndIsEnabled(
            environmentName,
            configuration);

        Assert.That(result, Is.False);
    }

    [TestCase("Development")]
    [TestCase("Production")]
    [TestCase("Staging")]
    public void ValidateAndIsEnabled_should_reject_enabling_documentation_outside_demo(
        string environmentName)
    {
        var configuration = CreateConfiguration(enabled: true);

        Assert.Throws<InvalidOperationException>(() =>
            DemoApiDocumentationEnvironment.ValidateAndIsEnabled(
                environmentName,
                configuration));
    }

    [Test]
    public void ValidateAndIsEnabled_should_keep_documentation_disabled_in_demo_by_default()
    {
        var configuration = CreateConfiguration(enabled: false);

        var result = DemoApiDocumentationEnvironment.ValidateAndIsEnabled(
            "Demo",
            configuration);

        Assert.That(result, Is.False);
    }

    [Test]
    public void ValidateAndIsEnabled_should_enable_documentation_when_explicitly_enabled_in_demo()
    {
        var configuration = CreateConfiguration(enabled: true);

        var result = DemoApiDocumentationEnvironment.ValidateAndIsEnabled(
            "Demo",
            configuration);

        Assert.That(result, Is.True);
    }

    private static IConfiguration CreateConfiguration(bool enabled)
    {
        var values = new Dictionary<string, string?>
        {
            ["DemoApiDocumentation:Enabled"] = enabled.ToString()
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
