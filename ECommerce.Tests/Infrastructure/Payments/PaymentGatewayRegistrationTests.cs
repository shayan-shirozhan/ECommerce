using ECommerce.Application.Abstractions.Payments;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

    private static IConfiguration CreateConfiguration(bool zarinPalEnabled = false)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ECommerceDb"] = "Host=localhost;Database=ecommerce;Username=postgres;Password=postgres",
                ["Jwt:Issuer"] = "ECommerce.Tests",
                ["Jwt:Audience"] = "ECommerce.Tests",
                ["Jwt:SecretKey"] = new string('S', 64),
                ["ZarinPal:Enabled"] = zarinPalEnabled.ToString(),
                ["ZarinPal:MerchantId"] = "11111111-1111-1111-1111-111111111111",
                ["ZarinPal:ApiBaseUrl"] = "https://sandbox.zarinpal.com",
                ["ZarinPal:PaymentPageBaseUrl"] = "https://sandbox.zarinpal.com/pg/StartPay",
                ["ZarinPal:CallbackUrl"] = "https://localhost:5001/api/payments/zarinpal/callback"
            })
            .Build();
}
