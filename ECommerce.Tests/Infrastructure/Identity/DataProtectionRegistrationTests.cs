using ECommerce.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Infrastructure.Identity;

public sealed class DataProtectionRegistrationTests
{
    [Test]
    public void AddInfrastructure_should_persist_data_protection_keys_to_the_configured_directory()
    {
        var keysPath = Path.Combine(
            Path.GetTempPath(),
            "ecommerce-data-protection-tests",
            Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ECommerceDb"] = "Host=localhost;Database=ecommerce;Username=postgres;Password=postgres",
                ["Jwt:Issuer"] = "ECommerce.Tests",
                ["Jwt:Audience"] = "ECommerce.Tests",
                ["Jwt:SecretKey"] = new string('S', 64),
                ["DataProtection:ApplicationName"] = "ECommerce.Tests",
                ["DataProtection:KeysPath"] = keysPath
            })
            .Build();

        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddInfrastructure(configuration);

            using var serviceProvider = services.BuildServiceProvider();
            var protector = serviceProvider
                .GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("DataProtectionRegistrationTests");

            var protectedPayload = protector.Protect("persisted payload");
            Assert.That(protector.Unprotect(protectedPayload), Is.EqualTo("persisted payload"));
            Assert.That(Directory.Exists(keysPath), Is.True);
            Assert.That(Directory.GetFiles(keysPath, "*.xml"), Is.Not.Empty);
        }
        finally
        {
            if (Directory.Exists(keysPath))
                Directory.Delete(keysPath, recursive: true);
        }
    }
}
