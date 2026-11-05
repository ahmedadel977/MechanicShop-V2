using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Branches.Dtos;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Branches.Queries.GetBranchEmployees;

public sealed class GetBranchEmployeesQueryHandler(
    IAppDbContext context,
    ICurrentBranchContext currentBranchContext)
    : IRequestHandler<GetBranchEmployeesQuery, Result<List<BranchEmployeeDto>>>
{
    public async Task<Result<List<BranchEmployeeDto>>> Handle(
        GetBranchEmployeesQuery query,
        CancellationToken cancellationToken)
    {
        if (!currentBranchContext.IsCompanyWide && currentBranchContext.BranchId != query.BranchId)
        {
            return Error.Forbidden("Branch.Access.Denied", "You are not allowed to view employees in the requested branch.");
        }

        var branchExists = await context.Branches.AnyAsync(
            branch => branch.Id == query.BranchId && branch.IsActive,
            cancellationToken);
        if (!branchExists)
        {
            return Error.NotFound("Branch.NotFound", "The branch does not exist or is inactive.");
        }

        var employees = await context.EmployeeBranchMemberships.AsNoTracking()
            .Where(membership => membership.BranchId == query.BranchId)
            .Join(context.Employees.AsNoTracking(),
                membership => membership.EmployeeId,
                employee => employee.Id,
                (membership, employee) => new BranchEmployeeDto(
                    employee.Id,
                    employee.FirstName + " " + employee.LastName,
                    employee.Role,
                    employee.AccessScope,
                    membership.IsDefault))
            .OrderBy(employee => employee.Name)
            .ToListAsync(cancellationToken);
        return employees;
    }
}
