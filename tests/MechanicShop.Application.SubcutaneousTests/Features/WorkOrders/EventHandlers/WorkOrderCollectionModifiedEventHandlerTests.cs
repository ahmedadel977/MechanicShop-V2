using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.EventHandlers;
using MechanicShop.Domain.Workorders.Events;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.EventHandlers;

public class WorkOrderCollectionModifiedEventHandlerTests
{
    [Fact]
    public async Task Handle_WhenCollectionIsModified_ShouldNotifySubscribersAndForwardCancellationToken()
    {
        using var cancellationSource = new CancellationTokenSource();
        var notifier = new RecordingWorkOrderNotifier();
        var handler = new WorkOrderCollectionModifiedEventHandler(notifier);

        var branchId = Guid.NewGuid();
        var laborId = Guid.NewGuid();
        await handler.Handle(new WorkOrderCollectionModified(branchId, laborId), cancellationSource.Token);

        Assert.Equal(1, notifier.CallCount);
        Assert.Equal(cancellationSource.Token, notifier.CancellationToken);
        Assert.Equal(branchId, notifier.BranchId);
        Assert.Equal(laborId, notifier.LaborId);
    }

    [Fact]
    public async Task Handle_WhenNotifierFails_ShouldPropagateFailureWithoutRetrying()
    {
        var notifier = new RecordingWorkOrderNotifier(new InvalidOperationException("Notifier failed."));
        var handler = new WorkOrderCollectionModifiedEventHandler(notifier);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new WorkOrderCollectionModified(Guid.NewGuid(), null), CancellationToken.None));

        Assert.Equal("Notifier failed.", exception.Message);
        Assert.Equal(1, notifier.CallCount);
    }

    private sealed class RecordingWorkOrderNotifier : IWorkOrderNotifier
    {
        private readonly Exception? _exception;

        public RecordingWorkOrderNotifier(Exception? exception = null)
        {
            _exception = exception;
        }

        public int CallCount { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public Guid BranchId { get; private set; }
        public Guid? LaborId { get; private set; }

        public Task NotifyWorkOrdersChangedAsync(Guid branchId, Guid? laborId, CancellationToken ct = default)
        {
            CallCount++;
            CancellationToken = ct;
            BranchId = branchId;
            LaborId = laborId;
            if (_exception is not null)
            {
                return Task.FromException(_exception);
            }

            return Task.CompletedTask;
        }
    }
}
