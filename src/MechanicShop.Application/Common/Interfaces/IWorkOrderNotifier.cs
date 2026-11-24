namespace MechanicShop.Application.Common.Interfaces;

public interface IWorkOrderNotifier
{
    Task NotifyWorkOrdersChangedAsync(Guid branchId, Guid? laborId, CancellationToken ct = default);
}
