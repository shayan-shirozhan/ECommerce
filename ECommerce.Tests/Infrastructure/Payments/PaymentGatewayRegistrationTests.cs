using ECommerce.Application.Abstractions.Payments;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Infrastructure.Payments;

public sealed class PaymentGatewayRegistrationTests
{
    [Test]
    public void AddInfrastructure_should_not_register_mock_gateway_by_default()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(CreateConfiguration());

        Assert.That(
            services.Any(descriptor => descriptor.ServiceType == typeof(IPaymentGateway)),
            Is.False);
    }

    [Test]
    public void AddInfrastructure_should_register_mock_gateway_only_when_explicitly_enabled()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(CreateConfiguration(), enableMockPaymentGateway: true);

        var descriptor = services.SingleOrDefault(
            item => item.ServiceType == typeof(IPaymentGateway));

        Assert.That(descriptor, Is.Not.Null);
        Assert.That(descriptor!.ImplementationType, Is.EqualTo(typeof(MockPaymentGateway)));
    }

    private static IConfiguration CreateConfiguration()
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ECommerceDb"] = "Host=localhost;Database=ecommerce;Username=postgres;Password=postgres",
                ["Jwt:Issuer"] = "ECommerce.Tests",
                ["Jwt:Audience"] = "ECommerce.Tests",
                ["Jwt:SecretKey"] = new string('S', 64)
            })
            .Build();
}
