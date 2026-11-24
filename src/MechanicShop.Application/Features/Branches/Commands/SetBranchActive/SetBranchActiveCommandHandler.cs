using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Branches;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Branches.Commands.SetBranchActive;

public sealed class SetBranchActiveCommandHandler(
    IAppDbContext context,
    ICurrentEmployeeAccess currentEmployeeAccess)
    : IRequestHandler<SetBranchActiveCommand, Result<Updated>>
{
    public async Task<Result<Updated>> Handle(
        SetBranchActiveCommand command,
        CancellationToken cancellationToken)
    {
        var actor = await currentEmployeeAccess.GetCurrentAsync(cancellationToken);
        if (actor?.Role != Role.Manager || actor.AccessScope != AccessScope.Company)
        {
            return Error.Forbidden("Branch.CompanyScope.Required", "Only a company-scoped manager can change branch status.");
        }

        var branch = await context.Branches.SingleOrDefaultAsync(
            item => item.Id == command.BranchId,
            cancellationToken);
        if (branch is null)
        {
            return Error.NotFound("Branch.NotFound", "The branch does not exist.");
        }

        if (!command.IsActive && await context.EmployeeBranchMemberships.AnyAsync(
                membership => membership.BranchId == command.BranchId && membership.IsDefault,
                cancellationToken))
        {
            return Error.Conflict("Branch.DefaultMembership.Exists", "Move employees to another default branch before deactivating this branch.");
        }

        branch.SetActive(command.IsActive);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Updated;
    }
}
