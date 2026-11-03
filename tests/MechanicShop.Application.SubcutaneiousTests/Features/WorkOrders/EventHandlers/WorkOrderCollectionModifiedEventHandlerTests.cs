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

        await handler.Handle(new WorkOrderCollectionModified(), cancellationSource.Token);

        Assert.Equal(1, notifier.CallCount);
        Assert.Equal(cancellationSource.Token, notifier.CancellationToken);
    }

    [Fact]
    public async Task Handle_WhenNotifierFails_ShouldPropagateFailureWithoutRetrying()
    {
        var notifier = new RecordingWorkOrderNotifier(new InvalidOperationException("Notifier failed."));
        var handler = new WorkOrderCollectionModifiedEventHandler(notifier);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(new WorkOrderCollectionModified(), CancellationToken.None));

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

        public Task NotifyWorkOrdersChangedAsync(CancellationToken ct = default)
        {
            CallCount++;
            CancellationToken = ct;
            if (_exception is not null)
            {
                return Task.FromException(_exception);
            }

            return Task.CompletedTask;
        }
    }
}
