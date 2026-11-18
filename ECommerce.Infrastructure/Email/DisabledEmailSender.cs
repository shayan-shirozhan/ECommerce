using ECommerce.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace ECommerce.Infrastructure.Email;

internal sealed class DisabledEmailSender(ILogger<DisabledEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogWarning(
            "Password reset email delivery is not configured. No email was sent. Register a production IEmailSender implementation to enable delivery.");
        return Task.CompletedTask;
    }
}
