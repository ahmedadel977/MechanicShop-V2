using System.Collections.Concurrent;
using MechanicShop.Application.Common.Interfaces;

namespace MechanicShop.Application.SubcutaneousTests.Common;

public sealed class RecordingNotificationService : INotificationService
{
    private readonly ConcurrentQueue<string> _emails = new();
    private readonly ConcurrentQueue<string> _smsMessages = new();

    public IReadOnlyCollection<string> Emails => _emails.ToArray();
    public IReadOnlyCollection<string> SmsMessages => _smsMessages.ToArray();

    public Task SendEmailAsync(string to, CancellationToken cancellationToken = default)
    {
        _emails.Enqueue(to);
        return Task.CompletedTask;
    }

    public Task SendSmsAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        _smsMessages.Enqueue(phoneNumber);
        return Task.CompletedTask;
    }

    public void Clear()
    {
        while (_emails.TryDequeue(out _)) { }
        while (_smsMessages.TryDequeue(out _)) { }
    }
}
