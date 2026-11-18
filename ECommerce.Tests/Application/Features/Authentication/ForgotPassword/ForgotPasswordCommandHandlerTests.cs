using ECommerce.Application;
using ECommerce.Application.Abstractions.Email;
using ECommerce.Application.Abstractions.Identity;
using ECommerce.Application.Features.Authentication.ForgotPassword;
using ECommerce.Application.Options;
using ECommerce.Infrastructure;
using ECommerce.Tests.Infrastructure.Fixtures;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECommerce.Tests.Application.Features.Authentication.ForgotPassword;

public sealed class ForgotPasswordCommandHandlerTests : PostgresTestBase
{
    private const string GenericMessage =
        "If an account exists for this email address, password reset instructions will be sent.";

    [Test]
    public async Task Handle_should_send_reset_link_and_return_generic_response_for_existing_account()
    {
        var email = $"forgot-{Guid.NewGuid():N}@example.test";
        var emailSender = new TestEmailSender();
        using var provider = CreateProvider(emailSender);

        using (var scope = provider.CreateScope())
        {
            var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            await identityService.CreateUserAsync(email, "Password123!", "Test", "User");
        }

        var response = await SendAsync(provider, new ForgotPasswordCommand(email));

        Assert.Multiple(() =>
        {
            Assert.That(response.Message, Is.EqualTo(GenericMessage));
            Assert.That(emailSender.Messages, Has.Count.EqualTo(1));
            Assert.That(emailSender.Messages[0].To, Is.EqualTo(email));
            Assert.That(emailSender.Messages[0].Subject, Is.EqualTo("Reset your ECommerce password"));
            Assert.That(emailSender.Messages[0].Body, Does.Contain("http://localhost:5173/reset-password?"));
            Assert.That(emailSender.Messages[0].Body, Does.Contain($"email={Uri.EscapeDataString(email)}"));
            Assert.That(emailSender.Messages[0].Body, Does.Contain("&token="));
            Assert.That(emailSender.Messages[0].Body, Does.Contain("30 minutes"));
        });
    }

    [Test]
    public async Task Handle_should_return_same_response_without_sending_email_for_unknown_account()
    {
        var emailSender = new TestEmailSender();
        using var provider = CreateProvider(emailSender);

        var response = await SendAsync(
            provider,
            new ForgotPasswordCommand($"unknown-{Guid.NewGuid():N}@example.test"));

        Assert.Multiple(() =>
        {
            Assert.That(response.Message, Is.EqualTo(GenericMessage));
            Assert.That(emailSender.Messages, Is.Empty);
        });
    }

    [Test]
    public async Task Handle_should_keep_generic_response_when_email_delivery_fails()
    {
        var email = $"delivery-failure-{Guid.NewGuid():N}@example.test";
        var emailSender = new TestEmailSender { ShouldFail = true };
        using var provider = CreateProvider(emailSender);

        using (var scope = provider.CreateScope())
        {
            var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            await identityService.CreateUserAsync(email, "Password123!", "Test", "User");
        }

        var response = await SendAsync(provider, new ForgotPasswordCommand(email));

        Assert.That(response.Message, Is.EqualTo(GenericMessage));
    }

    private ServiceProvider CreateProvider(TestEmailSender emailSender)
    {
        var configuration = new ConfigurationManager();
        configuration["ConnectionStrings:ECommerceDb"] = ConnectionString;
        configuration["Jwt:Issuer"] = "ECommerce.Tests";
        configuration["Jwt:Audience"] = "ECommerce.Tests";
        configuration["Jwt:ExpirationMinutes"] = "60";
        configuration["Jwt:SecretKey"] = new string('t', 32);
        configuration[$"{PasswordResetEmailOptions.SectionName}:FrontendUrl"] = "http://localhost:5173/reset-password";
        configuration[$"{PasswordResetEmailOptions.SectionName}:TokenLifespanMinutes"] = "30";

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration, enableDevelopmentEmailSender: true);
        services.AddSingleton<IEmailSender>(emailSender);

        return services.BuildServiceProvider();
    }

    private static async Task<ForgotPasswordResponse> SendAsync(
        IServiceProvider provider,
        ForgotPasswordCommand command)
    {
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        return await sender.Send(command);
    }

    private sealed class TestEmailSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];
        public bool ShouldFail { get; init; }

        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ShouldFail)
                throw new InvalidOperationException("Simulated email delivery failure.");

            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
