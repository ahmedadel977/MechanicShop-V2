using MechanicShop.Contracts.Common;

namespace MechanicShop.Contracts.Requests.Branches;

public sealed class SetEmployeeAccessScopeRequest
{
    public AccessScope AccessScope { get; set; }
}
