using System.Collections.Concurrent;
using ECommerce.Application.Abstractions.Email;

namespace ECommerce.Tests.API.Fixtures;

public sealed class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public IReadOnlyList<EmailMessage> Messages => _messages.ToArray();

    public void Clear()
    {
        while (_messages.TryDequeue(out _))
        {
        }
    }

    public Task SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }
}
