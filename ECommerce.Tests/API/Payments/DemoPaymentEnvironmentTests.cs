using ECommerce.API.Payments;
using Microsoft.Extensions.Configuration;

namespace ECommerce.Tests.API.Payments;

public sealed class DemoPaymentEnvironmentTests
{
    [TestCase("Development")]
    [TestCase("Production")]
    [TestCase("Staging")]
    public void ValidateAndIsEnabled_should_return_false_outside_demo_when_flag_is_disabled(
        string environmentName)
    {
        var configuration = CreateConfiguration();

        var result = DemoPaymentEnvironment.ValidateAndIsEnabled(environmentName, configuration);

        Assert.That(result, Is.False);
    }

    [TestCase("Development")]
    [TestCase("Production")]
    [TestCase("Staging")]
    public void ValidateAndIsEnabled_should_reject_demo_flag_outside_demo(string environmentName)
    {
        var configuration = CreateConfiguration(demoPaymentsEnabled: true);

        Assert.Throws<InvalidOperationException>(() =>
            DemoPaymentEnvironment.ValidateAndIsEnabled(environmentName, configuration));
    }

    [Test]
    public void ValidateAndIsEnabled_should_require_explicit_flag_inside_demo()
    {
        var configuration = CreateConfiguration();

        Assert.Throws<InvalidOperationException>(() =>
            DemoPaymentEnvironment.ValidateAndIsEnabled("Demo", configuration));
    }

    [Test]
    public void ValidateAndIsEnabled_should_require_dedicated_demo_connection_string()
    {
        var configuration = CreateConfiguration(
            demoPaymentsEnabled: true,
            includeDemoConnectionString: false);

        Assert.Throws<InvalidOperationException>(() =>
            DemoPaymentEnvironment.ValidateAndIsEnabled("Demo", configuration));
    }

    [Test]
    public void ValidateAndIsEnabled_should_reject_same_demo_and_application_connection_strings()
    {
        var configuration = CreateConfiguration(
            demoPaymentsEnabled: true,
            sameConnectionStrings: true);

        Assert.Throws<InvalidOperationException>(() =>
            DemoPaymentEnvironment.ValidateAndIsEnabled("Demo", configuration));
    }

    [Test]
    public void ValidateAndIsEnabled_should_reject_same_database_target_even_when_credentials_differ()
    {
        var configuration = CreateConfiguration(
            demoPaymentsEnabled: true,
            sameDatabaseDifferentCredentials: true);

        Assert.Throws<InvalidOperationException>(() =>
            DemoPaymentEnvironment.ValidateAndIsEnabled("Demo", configuration));
    }

    [Test]
    public void ValidateAndIsEnabled_should_reject_zarinpal_in_demo_environment()
    {
        var configuration = CreateConfiguration(
            demoPaymentsEnabled: true,
            zarinPalEnabled: true);

        Assert.Throws<InvalidOperationException>(() =>
            DemoPaymentEnvironment.ValidateAndIsEnabled("Demo", configuration));
    }

    [Test]
    public void ValidateAndIsEnabled_should_enable_demo_when_all_boundaries_are_valid()
    {
        var configuration = CreateConfiguration(demoPaymentsEnabled: true);

        var result = DemoPaymentEnvironment.ValidateAndIsEnabled("Demo", configuration);

        Assert.That(result, Is.True);
    }

    private static IConfiguration CreateConfiguration(
        bool demoPaymentsEnabled = false,
        bool zarinPalEnabled = false,
        bool includeDemoConnectionString = true,
        bool sameConnectionStrings = false,
        bool sameDatabaseDifferentCredentials = false)
    {
        const string applicationConnectionString =
            "Host=localhost;Database=ecommerce;Username=postgres;Password=postgres";
        var demoConnectionString = sameConnectionStrings
            ? applicationConnectionString
            : sameDatabaseDifferentCredentials
                ? "Host=localhost;Port=5432;Database=ecommerce;Username=demo;Password=demo-password"
                : "Host=localhost;Database=ecommerce_demo;Username=postgres;Password=postgres";

        var values = new Dictionary<string, string?>
        {
            ["DemoPayments:Enabled"] = demoPaymentsEnabled.ToString(),
            ["ZarinPal:Enabled"] = zarinPalEnabled.ToString(),
            ["ConnectionStrings:ECommerceDb"] = applicationConnectionString
        };

        if (includeDemoConnectionString)
            values["ConnectionStrings:DemoECommerceDb"] = demoConnectionString;

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
