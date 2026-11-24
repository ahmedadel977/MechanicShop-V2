using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;

namespace MechanicShop.Domain.Employees;

public sealed class Employee : AuditableEntity
{
    public string FirstName { get; }
    public string LastName { get; }
    public Role Role { get; }
    public AccessScope AccessScope { get; private set; }
    public string FullName => $"{FirstName} {LastName}";

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

    private Employee()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    { }

    private Employee(Guid id, string firstName, string lastName, Role role, AccessScope accessScope)
        : base(id)
    {
        FirstName = firstName;
        LastName = lastName;
        Role = role;
        AccessScope = accessScope;
    }

    public static Result<Employee> Create(Guid id, string firstName, string lastName, Role role, AccessScope accessScope = AccessScope.Branch)
    {
        if (id == Guid.Empty)
        {
            return EmployeeErrors.IdRequired;
        }

        if (string.IsNullOrWhiteSpace(firstName))
        {
            return EmployeeErrors.FirstNameRequired;
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            return EmployeeErrors.LastNameRequired;
        }

        if (!Enum.IsDefined(role))
        {
            return EmployeeErrors.RoleInvalid;
        }

        if (!Enum.IsDefined(accessScope) || (accessScope == AccessScope.Company && role != Role.Manager))
        {
            return EmployeeErrors.AccessScopeInvalid;
        }

        return new Employee(id, firstName.Trim(), lastName.Trim(), role, accessScope);
    }

    public Result<Updated> SetAccessScope(AccessScope accessScope)
    {
        if (!Enum.IsDefined(accessScope) || (accessScope == AccessScope.Company && Role != Role.Manager))
        {
            return EmployeeErrors.AccessScopeInvalid;
        }

        AccessScope = accessScope;
        return Result.Updated;
    }
}
