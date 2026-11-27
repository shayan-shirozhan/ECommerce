using ECommerce.Application.Abstractions.Email;
using ECommerce.Infrastructure;
using ECommerce.Infrastructure.Email;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ECommerce.Tests.Infrastructure.Email;

public sealed class SmtpEmailRegistrationTests
{
    [Test]
    public void AddInfrastructure_should_register_smtp_sender_when_explicitly_enabled()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(CreateConfiguration(smtpEnabled: true));

        var descriptor = services.Single(item => item.ServiceType == typeof(IEmailSender));

        Assert.That(descriptor.ImplementationType, Is.EqualTo(typeof(SmtpEmailSender)));
    }

    [Test]
    public void AddInfrastructure_should_register_smtp_sender_for_demo_with_tls()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(
            CreateConfiguration(smtpEnabled: true, demoPaymentsEnabled: true),
            enableDemoPaymentGateway: true,
            connectionStringName: "DemoECommerceDb",
            requireSmtpEmailSender: true);

        var descriptor = services.Single(item => item.ServiceType == typeof(IEmailSender));

        Assert.That(descriptor.ImplementationType, Is.EqualTo(typeof(SmtpEmailSender)));
    }

    [Test]
    public void AddInfrastructure_should_keep_development_logger_when_smtp_is_disabled_in_development()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(CreateConfiguration(smtpEnabled: false), enableDevelopmentEmailSender: true);

        var descriptor = services.Single(item => item.ServiceType == typeof(IEmailSender));

        Assert.That(descriptor.ImplementationType, Is.EqualTo(typeof(DevelopmentEmailSender)));
    }

    [Test]
    public void AddInfrastructure_should_keep_disabled_sender_when_smtp_is_disabled_outside_development()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(CreateConfiguration(smtpEnabled: false));

        var descriptor = services.Single(item => item.ServiceType == typeof(IEmailSender));

        Assert.That(descriptor.ImplementationType, Is.EqualTo(typeof(DisabledEmailSender)));
    }

    [Test]
    public void AddInfrastructure_should_require_smtp_when_demo_email_delivery_is_required()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(
                CreateConfiguration(smtpEnabled: false, demoPaymentsEnabled: true),
                enableDemoPaymentGateway: true,
                connectionStringName: "DemoECommerceDb",
                requireSmtpEmailSender: true));
    }

    [Test]
    public void AddInfrastructure_should_reject_insecure_smtp_outside_development()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(
                CreateConfiguration(smtpEnabled: true, security: SmtpSecurityMode.None)));
    }

    [Test]
    public void AddInfrastructure_should_require_frontend_reset_url_when_smtp_is_enabled()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(smtpEnabled: true);
        configuration["PasswordResetEmail:FrontendUrl"] = string.Empty;

        Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(configuration));
    }

    [Test]
    public void AddInfrastructure_should_require_https_reset_url_outside_development()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration(smtpEnabled: true);
        configuration["PasswordResetEmail:FrontendUrl"] = "http://shop.example.test/reset-password";

        Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(configuration));
    }

    [Test]
    public void AddInfrastructure_should_allow_mailpit_plaintext_smtp_in_development()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = CreateConfiguration(smtpEnabled: true, security: SmtpSecurityMode.None);
        configuration["PasswordResetEmail:FrontendUrl"] = "http://localhost:5173/reset-password";
        services.AddInfrastructure(
            configuration,
            enableDevelopmentEmailSender: true);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<SmtpEmailOptions>>().Value;
        var sender = provider.GetRequiredService<IEmailSender>();

        Assert.Multiple(() =>
        {
            Assert.That(options.Host, Is.EqualTo("mailpit"));
            Assert.That(sender, Is.TypeOf<SmtpEmailSender>());
        });
    }

    private static IConfiguration CreateConfiguration(
        bool smtpEnabled,
        SmtpSecurityMode security = SmtpSecurityMode.StartTls,
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
                ["PasswordResetEmail:FrontendUrl"] = "https://shop.example.test/reset-password",
                ["SmtpEmail:Enabled"] = smtpEnabled.ToString(),
                ["SmtpEmail:Host"] = "mailpit",
                ["SmtpEmail:Port"] = "1025",
                ["SmtpEmail:Security"] = security.ToString(),
                ["SmtpEmail:FromAddress"] = "no-reply@ecommerce.test",
                ["SmtpEmail:FromName"] = "ECommerce",
                ["SmtpEmail:TimeoutSeconds"] = "15"
            })
            .Build();
}
