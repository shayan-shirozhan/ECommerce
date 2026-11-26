using ECommerce.Application.Abstractions.Payments;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ECommerce.Tests.Infrastructure.Payments;

public sealed class PaymentGatewayRegistrationTests
{
    [Test]
    public void AddInfrastructure_should_not_register_any_payment_gateway_by_default()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(CreateConfiguration());

        Assert.Multiple(() =>
        {
            Assert.That(
                services.Any(descriptor => descriptor.ServiceType == typeof(IPaymentGateway)),
                Is.False);
            Assert.That(
                services.Any(descriptor => descriptor.ServiceType == typeof(IMockPaymentGateway)),
                Is.False);
        });
    }

    [Test]
    public void AddInfrastructure_should_register_mock_gateway_only_when_explicitly_enabled()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(CreateConfiguration(), enableMockPaymentGateway: true);

        var descriptor = services.SingleOrDefault(
            item => item.ServiceType == typeof(IMockPaymentGateway));

        Assert.That(descriptor, Is.Not.Null);
        Assert.That(descriptor!.ImplementationType, Is.EqualTo(typeof(MockPaymentGateway)));
        Assert.That(
            services.Any(item => item.ServiceType == typeof(IPaymentGateway)),
            Is.False);
    }

    [Test]
    public void AddInfrastructure_should_register_deterministic_demo_gateway_only_when_explicitly_enabled()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(demoPaymentsEnabled: true);

        services.AddInfrastructure(
            configuration,
            enableDemoPaymentGateway: true,
            connectionStringName: "DemoECommerceDb");

        var descriptor = services.SingleOrDefault(
            item => item.ServiceType == typeof(IMockPaymentGateway));

        Assert.Multiple(() =>
        {
            Assert.That(descriptor, Is.Not.Null);
            Assert.That(descriptor!.ImplementationType, Is.EqualTo(typeof(DemoPaymentGateway)));
            Assert.That(
                services.Any(item => item.ServiceType == typeof(IPaymentGateway)),
                Is.False);
        });
    }

    [Test]
    public void AddInfrastructure_should_reject_demo_gateway_without_explicit_flag()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(
                CreateConfiguration(),
                enableDemoPaymentGateway: true,
                connectionStringName: "DemoECommerceDb"));
    }

    [Test]
    public void AddInfrastructure_should_reject_demo_flag_when_demo_gateway_registration_is_disabled()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(CreateConfiguration(demoPaymentsEnabled: true)));
    }

    [Test]
    public void AddInfrastructure_should_reject_demo_gateway_when_using_normal_database_connection_name()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(
                CreateConfiguration(demoPaymentsEnabled: true),
                enableDemoPaymentGateway: true));
    }

    [Test]
    public void AddInfrastructure_should_reject_demo_gateway_when_zarinpal_is_enabled()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(
                CreateConfiguration(zarinPalEnabled: true, demoPaymentsEnabled: true),
                enableDemoPaymentGateway: true,
                connectionStringName: "DemoECommerceDb"));
    }

    [Test]
    public void AddInfrastructure_should_not_combine_demo_gateway_with_development_services()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(
                CreateConfiguration(demoPaymentsEnabled: true),
                enableMockPaymentGateway: true,
                enableDemoPaymentGateway: true,
                connectionStringName: "DemoECommerceDb"));
    }

    [Test]
    public void AddInfrastructure_should_register_zarinpal_gateway_only_when_enabled_and_configured()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(CreateConfiguration(zarinPalEnabled: true));

        Assert.That(
            services.Any(item => item.ServiceType == typeof(IPaymentGateway)),
            Is.True);
        Assert.That(
            services.Any(item => item.ServiceType == typeof(IMockPaymentGateway)),
            Is.False);

        services.AddLogging();
        using var provider = services.BuildServiceProvider();
        Assert.That(provider.GetRequiredService<IPaymentGateway>(), Is.Not.Null);
    }

    [Test]
    public void AddInfrastructure_should_reject_zarinpal_timeout_that_reaches_retry_lease()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(CreateConfiguration(zarinPalEnabled: true, timeoutSeconds: 60));
        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IPaymentGateway>());
    }

    private static IConfiguration CreateConfiguration(
        bool zarinPalEnabled = false,
        int timeoutSeconds = 15,
        bool demoPaymentsEnabled = false)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ECommerceDb"] = "Host=localhost;Database=ecommerce;Username=postgres;Password=postgres",
                ["ConnectionStrings:DemoECommerceDb"] = "Host=localhost;Database=ecommerce_demo;Username=postgres;Password=postgres",
                ["DemoPayments:Enabled"] = demoPaymentsEnabled.ToString(),
                ["Jwt:Issuer"] = "ECommerce.Tests",
                ["Jwt:Audience"] = "ECommerce.Tests",
                ["Jwt:SecretKey"] = new string('S', 64),
                ["ZarinPal:Enabled"] = zarinPalEnabled.ToString(),
                ["ZarinPal:TimeoutSeconds"] = timeoutSeconds.ToString(),
                ["ZarinPal:MerchantId"] = "11111111-1111-1111-1111-111111111111",
                ["ZarinPal:ApiBaseUrl"] = "https://sandbox.zarinpal.com",
                ["ZarinPal:PaymentPageBaseUrl"] = "https://sandbox.zarinpal.com/pg/StartPay",
                ["ZarinPal:CallbackUrl"] = "https://localhost:5001/api/payments/zarinpal/callback"
            })
            .Build();
}
