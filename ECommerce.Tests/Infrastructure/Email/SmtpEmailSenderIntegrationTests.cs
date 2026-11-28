using System.Text.Json;
using DotNet.Testcontainers.Builders;
using ECommerce.Application.Abstractions.Email;
using ECommerce.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Infrastructure.Email;

[TestFixture]
[NonParallelizable]
public sealed class SmtpEmailSenderIntegrationTests
{
    private const string MailpitImage =
        "ghcr.io/axllent/mailpit:v1.34.0@sha256:23a2ef85bc50853ab3093645d1d64679afd8887e89da5e48a88940c172591a4e";
    private const ushort SmtpPort = 1025;
    private const ushort HttpPort = 8025;

    [Test]
    public async Task SendAsync_should_deliver_a_real_smtp_message_to_mailpit()
    {
        await using var mailpit = new ContainerBuilder(MailpitImage)
            .WithPortBinding(SmtpPort, true)
            .WithPortBinding(HttpPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilInternalTcpPortIsAvailable(SmtpPort)
                .UntilHttpRequestIsSucceeded(request => request
                    .ForPort(HttpPort)
                    .ForPath("/api/v1/info")))
            .Build();

        await mailpit.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:ECommerceDb"] =
                    "Host=127.0.0.1;Database=ecommerce_email_test;Username=postgres;Password=postgres",
                ["Jwt:Issuer"] = "ECommerce.Email.Tests",
                ["Jwt:Audience"] = "ECommerce.Email.Tests",
                ["Jwt:SecretKey"] = new string('E', 64),
                ["PasswordResetEmail:FrontendUrl"] = "http://localhost:5173/reset-password",
                ["SmtpEmail:Enabled"] = "true",
                ["SmtpEmail:Host"] = mailpit.Hostname,
                ["SmtpEmail:Port"] = mailpit.GetMappedPublicPort(SmtpPort).ToString(),
                ["SmtpEmail:Security"] = "None",
                ["SmtpEmail:FromAddress"] = "no-reply@ecommerce.test",
                ["SmtpEmail:FromName"] = "ECommerce Integration Tests",
                ["SmtpEmail:TimeoutSeconds"] = "15"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration, enableDevelopmentEmailSender: true);

        using var serviceProvider = services.BuildServiceProvider();
        await using var scope = serviceProvider.CreateAsyncScope();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();

        const string recipient = "smtp-recipient@integration.test";
        const string subject = "Real SMTP delivery verification";
        const string body = "The MailKit sender delivered this message through the Mailpit SMTP listener.";

        await emailSender.SendAsync(new EmailMessage(recipient, subject, body));

        using var mailpitApi = new HttpClient
        {
            BaseAddress = new UriBuilder(
                Uri.UriSchemeHttp,
                mailpit.Hostname,
                mailpit.GetMappedPublicPort(HttpPort)).Uri
        };

        string? messageJson = null;
        var deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            using var latestMessageResponse = await mailpitApi.GetAsync("/api/v1/message/latest");
            if (latestMessageResponse.IsSuccessStatusCode)
            {
                messageJson = await latestMessageResponse.Content.ReadAsStringAsync();
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        Assert.That(messageJson, Is.Not.Null, "Mailpit did not receive the SMTP message within ten seconds.");

        using var messageDocument = JsonDocument.Parse(messageJson!);
        var message = messageDocument.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(GetPropertyIgnoreCase(message, "Subject").GetString(), Is.EqualTo(subject));
            Assert.That(GetPropertyIgnoreCase(message, "Text").GetString(), Does.Contain(body));
            var recipientAddress = GetPropertyIgnoreCase(
                GetPropertyIgnoreCase(message, "To").EnumerateArray().Single(),
                "Address").GetString();
            Assert.That(recipientAddress, Is.EqualTo(recipient));
        });
    }

    private static JsonElement GetPropertyIgnoreCase(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }

        throw new AssertionException($"Mailpit response did not contain '{propertyName}'.");
    }
}
