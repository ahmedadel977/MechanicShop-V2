using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Branches;

public sealed class EmployeeBranchMembership
{
    public Guid EmployeeId { get; private set; }
    public Guid BranchId { get; private set; }
    public bool IsDefault { get; private set; }

    private EmployeeBranchMembership()
    {
    }

    private EmployeeBranchMembership(Guid employeeId, Guid branchId, bool isDefault)
    {
        EmployeeId = employeeId;
        BranchId = branchId;
        IsDefault = isDefault;
    }

    public static Result<EmployeeBranchMembership> Create(Guid employeeId, Guid branchId, bool isDefault)
    {
        if (employeeId == Guid.Empty)
        {
            return BranchErrors.EmployeeIdRequired;
        }

        if (branchId == Guid.Empty)
        {
            return BranchErrors.IdRequired;
        }

        return new EmployeeBranchMembership(employeeId, branchId, isDefault);
    }

    public Result<Updated> SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
        return Result.Updated;
    }
}
