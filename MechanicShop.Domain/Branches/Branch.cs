using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Branches;

public sealed class Branch : AuditableEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private Branch()
    {
    }

    private Branch(Guid id, string name, string code)
        : base(id)
    {
        Name = name;
        Code = code;
        IsActive = true;
    }

    public static Result<Branch> Create(Guid id, string name, string code)
    {
        if (id == Guid.Empty)
        {
            return BranchErrors.IdRequired;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return BranchErrors.NameRequired;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            return BranchErrors.CodeRequired;
        }

        return new Branch(id, name.Trim(), code.Trim().ToUpperInvariant());
    }

    public Result<Updated> SetActive(bool isActive)
    {
        IsActive = isActive;
        return Result.Updated;
    }
}
