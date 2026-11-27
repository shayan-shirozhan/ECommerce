using ECommerce.Infrastructure.Email;

namespace ECommerce.Tests.Infrastructure.Email;

public sealed class SmtpEmailOptionsTests
{
    [Test]
    public void HasValidConfiguration_should_allow_disabled_smtp_without_connection_settings()
    {
        var options = new SmtpEmailOptions();

        Assert.That(options.HasValidConfiguration(allowInsecureTransport: false), Is.True);
    }

    [Test]
    public void HasValidConfiguration_should_accept_starttls_configuration_outside_development()
    {
        var options = CreateValidOptions(SmtpSecurityMode.StartTls);

        Assert.That(options.HasValidConfiguration(allowInsecureTransport: false), Is.True);
    }

    [Test]
    public void HasValidConfiguration_should_accept_ssl_on_connect_configuration_outside_development()
    {
        var options = CreateValidOptions(SmtpSecurityMode.SslOnConnect);

        Assert.That(options.HasValidConfiguration(allowInsecureTransport: false), Is.True);
    }

    [Test]
    public void HasValidConfiguration_should_reject_plaintext_smtp_outside_development()
    {
        var options = CreateValidOptions(SmtpSecurityMode.None);

        Assert.That(options.HasValidConfiguration(allowInsecureTransport: false), Is.False);
    }

    [Test]
    public void HasValidConfiguration_should_allow_plaintext_smtp_in_development_for_mailpit()
    {
        var options = CreateValidOptions(SmtpSecurityMode.None);

        Assert.That(options.HasValidConfiguration(allowInsecureTransport: true), Is.True);
    }

    [TestCase(0)]
    [TestCase(65536)]
    public void HasValidConfiguration_should_reject_invalid_ports(int port)
    {
        var options = CreateValidOptions();
        options.Port = port;

        Assert.That(options.HasValidConfiguration(allowInsecureTransport: false), Is.False);
    }

    [TestCase(0)]
    [TestCase(61)]
    public void HasValidConfiguration_should_reject_timeouts_outside_one_to_sixty_seconds(int timeoutSeconds)
    {
        var options = CreateValidOptions();
        options.TimeoutSeconds = timeoutSeconds;

        Assert.That(options.HasValidConfiguration(allowInsecureTransport: false), Is.False);
    }

    [Test]
    public void HasValidConfiguration_should_require_username_and_password_together()
    {
        var options = CreateValidOptions();
        options.Username = "smtp-user";

        Assert.That(options.HasValidConfiguration(allowInsecureTransport: false), Is.False);
    }

    [Test]
    public void HasValidConfiguration_should_reject_whitespace_only_smtp_credentials()
    {
        var options = CreateValidOptions();
        options.Username = " ";
        options.Password = " ";

        Assert.That(options.HasValidConfiguration(allowInsecureTransport: false), Is.False);
    }

    [Test]
    public void HasValidConfiguration_should_reject_invalid_sender_address()
    {
        var options = CreateValidOptions();
        options.FromAddress = "not-an-email";

        Assert.That(options.HasValidConfiguration(allowInsecureTransport: false), Is.False);
    }

    [Test]
    public void HasValidConfiguration_should_reject_sender_name_with_newlines()
    {
        var options = CreateValidOptions();
        options.FromName = "ECommerce\r\nBcc: attacker@example.test";

        Assert.That(options.HasValidConfiguration(allowInsecureTransport: false), Is.False);
    }

    private static SmtpEmailOptions CreateValidOptions(
        SmtpSecurityMode security = SmtpSecurityMode.StartTls)
        => new()
        {
            Enabled = true,
            Host = "smtp.example.test",
            Port = 587,
            Security = security,
            FromAddress = "no-reply@example.test",
            FromName = "ECommerce",
            TimeoutSeconds = 15
        };
}
