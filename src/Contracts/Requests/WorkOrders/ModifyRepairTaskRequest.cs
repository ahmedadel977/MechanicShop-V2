namespace MechanicShop.Contracts.Requests.WorkOrders;

public class ModifyRepairTaskRequest
{
    public Guid[] RepairTaskIds { get; set; } = [];
    public byte[]? RowVersion { get; set; }
}
