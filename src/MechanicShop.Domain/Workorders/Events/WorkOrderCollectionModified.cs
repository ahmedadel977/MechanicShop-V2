using MechanicShop.Domain.Common;

namespace MechanicShop.Domain.Workorders.Events;

public sealed class WorkOrderCollectionModified(Guid branchId, Guid? laborId) : DomainEvent
{
    public Guid BranchId { get; } = branchId;
    public Guid? LaborId { get; } = laborId;
}
