using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Branches;

public static class BranchErrors
{
    public static Error IdRequired => Error.Validation("Branch.Id.Required", "A branch ID is required.");
    public static Error NameRequired => Error.Validation("Branch.Name.Required", "A branch name is required.");
    public static Error CodeRequired => Error.Validation("Branch.Code.Required", "A branch code is required.");
    public static Error EmployeeIdRequired => Error.Validation("Branch.EmployeeId.Required", "An employee ID is required.");
    public static Error DefaultBranchRequired => Error.Validation("Branch.Default.Required", "The employee must have exactly one default branch.");
}
