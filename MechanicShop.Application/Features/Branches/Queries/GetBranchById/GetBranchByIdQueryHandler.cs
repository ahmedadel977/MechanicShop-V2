using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Branches.Dtos;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Branches.Queries.GetBranchById;

public sealed class GetBranchByIdQueryHandler(
    IAppDbContext context,
    ICurrentBranchContext currentBranchContext)
    : IRequestHandler<GetBranchByIdQuery, Result<BranchDto>>
{
    public async Task<Result<BranchDto>> Handle(GetBranchByIdQuery query, CancellationToken cancellationToken)
    {
        var branchQuery = context.Branches.AsNoTracking()
            .Where(branch => branch.Id == query.BranchId);
        if (!currentBranchContext.IsCompanyWide)
        {
            if (!currentBranchContext.EmployeeId.HasValue)
            {
                return Error.Forbidden("Branch.Employee.Invalid", "The current user is not linked to a valid employee.");
            }

            branchQuery = branchQuery.Where(branch => context.EmployeeBranchMemberships.Any(membership =>
                membership.EmployeeId == currentBranchContext.EmployeeId.Value
                && membership.BranchId == branch.Id));
        }

        var branch = await branchQuery.Select(item => new BranchDto(
            item.Id,
            item.Name,
            item.Code,
            item.IsActive)).SingleOrDefaultAsync(cancellationToken);
        return branch is null
            ? Error.NotFound("Branch.NotFound", "The branch does not exist or is not accessible.")
            : branch;
    }
}
