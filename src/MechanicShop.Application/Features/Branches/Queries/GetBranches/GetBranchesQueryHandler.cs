using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Branches.Dtos;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Branches.Queries.GetBranches;

public sealed class GetBranchesQueryHandler(
    IAppDbContext context,
    ICurrentBranchContext currentBranchContext)
    : IRequestHandler<GetBranchesQuery, Result<List<BranchDto>>>
{
    public async Task<Result<List<BranchDto>>> Handle(GetBranchesQuery request, CancellationToken cancellationToken)
    {
        var query = context.Branches.AsNoTracking();
        if (!currentBranchContext.IsCompanyWide)
        {
            if (!currentBranchContext.EmployeeId.HasValue)
            {
                return Error.Forbidden("Branch.Employee.Invalid", "The current user is not linked to a valid employee.");
            }

            query = query.Where(branch => context.EmployeeBranchMemberships.Any(membership =>
                membership.EmployeeId == currentBranchContext.EmployeeId.Value
                && membership.BranchId == branch.Id));
        }

        var branches = await query.OrderBy(branch => branch.Code)
            .Select(branch => new BranchDto(branch.Id, branch.Name, branch.Code, branch.IsActive))
            .ToListAsync(cancellationToken);
        return branches;
    }
}
