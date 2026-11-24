using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Common.Models;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Infrastructure.identity;

public sealed class CurrentEmployeeAccessService(
    IAppDbContext context,
    ICurrentBranchContext currentBranchContext) : ICurrentEmployeeAccess
{
    public Task<CurrentEmployeeAccess?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        if (!currentBranchContext.EmployeeId.HasValue)
        {
            return Task.FromResult<CurrentEmployeeAccess?>(null);
        }

        var employeeId = currentBranchContext.EmployeeId.Value;
        return context.Employees
            .Where(employee => employee.Id == employeeId)
            .Select(employee => new CurrentEmployeeAccess(
                employee.Id,
                employee.Role,
                employee.AccessScope))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
