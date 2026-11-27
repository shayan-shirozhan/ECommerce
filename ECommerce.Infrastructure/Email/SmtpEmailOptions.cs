using System.Net.Mail;

namespace ECommerce.Infrastructure.Email;

public sealed class SmtpEmailOptions
{
    public const string SectionName = "SmtpEmail";

    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public SmtpSecurityMode Security { get; set; } = SmtpSecurityMode.StartTls;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "ECommerce";

    public int TimeoutSeconds { get; set; } = 15;

    public bool HasValidConfiguration(bool allowInsecureTransport)
    {
        if (!Enabled)
            return true;

        var hasUsername = !string.IsNullOrWhiteSpace(Username);
        var hasPassword = !string.IsNullOrWhiteSpace(Password);
        var credentialsAreValid =
            (string.IsNullOrEmpty(Username) && string.IsNullOrEmpty(Password)) ||
            (hasUsername && hasPassword);

        return !string.IsNullOrWhiteSpace(Host) &&
               Port is > 0 and <= 65535 &&
               Enum.IsDefined(Security) &&
               (allowInsecureTransport || Security != SmtpSecurityMode.None) &&
               TimeoutSeconds is > 0 and <= 60 &&
               MailAddress.TryCreate(FromAddress, out var fromAddress) &&
               fromAddress is not null &&
               string.Equals(fromAddress.Address, FromAddress, StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(FromName) &&
               !FromName.Contains('\r') &&
               !FromName.Contains('\n') &&
               credentialsAreValid;
    }
}

public enum SmtpSecurityMode
{
    None,
    StartTls,
    SslOnConnect
}
