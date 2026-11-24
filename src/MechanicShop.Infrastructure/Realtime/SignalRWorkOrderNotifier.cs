using MechanicShop.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace MechanicShop.Infrastructure.Realtime;

public sealed class SignalRWorkOrderNotifier(IHubContext<WorkOrderHub> hubContext) : IWorkOrderNotifier
{
    private readonly IHubContext<WorkOrderHub> _hubContext = hubContext;

    public Task NotifyWorkOrdersChangedAsync(Guid branchId, Guid? laborId, CancellationToken ct = default)
    {
        var notifications = new List<Task>
        {
            _hubContext.Clients.Group(WorkOrderHub.BranchGroup(branchId))
                .SendAsync("WorkOrdersChanged", cancellationToken: ct)
        };

        if (laborId.HasValue)
        {
            notifications.Add(_hubContext.Clients.Group(WorkOrderHub.LaborGroup(laborId.Value))
                .SendAsync("WorkOrdersChanged", cancellationToken: ct));
        }

        return Task.WhenAll(notifications);
    }
}
