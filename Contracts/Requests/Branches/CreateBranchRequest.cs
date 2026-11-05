namespace MechanicShop.Contracts.Requests.Branches;

public sealed class CreateBranchRequest
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}
