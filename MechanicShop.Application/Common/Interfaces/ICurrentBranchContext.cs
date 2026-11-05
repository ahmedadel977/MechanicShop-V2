namespace MechanicShop.Application.Common.Interfaces;

public interface ICurrentBranchContext
{
    Guid? BranchId { get; }
    Guid? EmployeeId { get; }
    bool IsCompanyWide { get; }
    bool IsLabor { get; }
}
