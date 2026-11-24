using MechanicShop.Application.Common.Interfaces;

namespace MechanicShop.Application.SubcutaneousTests.Common;

public sealed class RecordingWorkOrderNotifier : IWorkOrderNotifier
{
    public int CallCount { get; private set; }
    public CancellationToken CancellationToken { get; private set; }
    public Guid BranchId { get; private set; }
    public Guid? LaborId { get; private set; }
    public Func<CancellationToken, Task>? OnNotify { get; set; }

    public async Task NotifyWorkOrdersChangedAsync(Guid branchId, Guid? laborId, CancellationToken ct = default)
    {
        if (OnNotify is not null)
        {
            await OnNotify(ct);
        }

        CallCount++;
        CancellationToken = ct;
        BranchId = branchId;
        LaborId = laborId;
    }

    public void Clear()
    {
        CallCount = 0;
        CancellationToken = default;
        BranchId = Guid.Empty;
        LaborId = null;
        OnNotify = null;
    }
}
