using MechanicShop.Domain.Identity;

namespace MechanicShop.Contracts.Requests.Branches;

public sealed class SetEmployeeAccessScopeRequest
{
    public AccessScope AccessScope { get; set; }
}
