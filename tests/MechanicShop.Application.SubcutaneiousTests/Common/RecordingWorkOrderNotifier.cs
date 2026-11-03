using MechanicShop.Application.Common.Interfaces;

namespace MechanicShop.Application.SubcutaneousTests.Common;

public sealed class RecordingWorkOrderNotifier : IWorkOrderNotifier
{
    public int CallCount { get; private set; }
    public CancellationToken CancellationToken { get; private set; }

    public Task NotifyWorkOrdersChangedAsync(CancellationToken ct = default)
    {
        CallCount++;
        CancellationToken = ct;
        return Task.CompletedTask;
    }

    public void Clear()
    {
        CallCount = 0;
        CancellationToken = default;
    }
}
