using ECommerce.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace ECommerce.Infrastructure.Email;

internal sealed class DevelopmentEmailSender(ILogger<DevelopmentEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The reset URL contains a single-use token. This implementation is registered only in Development.
        logger.LogInformation(
            "Development email. To: {Recipient}; Subject: {Subject}; Body: {Body}",
            message.To,
            message.Subject,
            message.Body);

        return Task.CompletedTask;
    }
}
