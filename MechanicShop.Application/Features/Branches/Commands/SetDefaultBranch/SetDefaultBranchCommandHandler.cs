using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Branches.Commands.SetDefaultBranch;

public sealed class SetDefaultBranchCommandHandler(
    IAppDbContext context,
    ICurrentBranchContext currentBranchContext)
    : IRequestHandler<SetDefaultBranchCommand, Result<Updated>>
{
    public async Task<Result<Updated>> Handle(
        SetDefaultBranchCommand command,
        CancellationToken cancellationToken)
    {
        if (!currentBranchContext.EmployeeId.HasValue)
        {
            return Error.Forbidden("Branch.Employee.Invalid", "The current user is not linked to a valid employee.");
        }

        var employeeId = currentBranchContext.EmployeeId.Value;
        var canUseBranch = await context.EmployeeBranchMemberships.AnyAsync(
                membership => membership.EmployeeId == employeeId && membership.BranchId == command.BranchId,
                cancellationToken)
            && await context.Branches.AnyAsync(
                branch => branch.Id == command.BranchId && branch.IsActive,
                cancellationToken);
        if (!canUseBranch)
        {
            return Error.Forbidden("Branch.Access.Denied", "The branch is inactive or not assigned to this account.");
        }

        await using var transaction = await context.BeginTransactionAsync(cancellationToken);
        try
        {
            var memberships = await context.EmployeeBranchMemberships
                .Where(membership => membership.EmployeeId == employeeId)
                .ToListAsync(cancellationToken);
            foreach (var membership in memberships)
            {
                membership.SetDefault(false);
            }

            await context.SaveChangesAsync(cancellationToken);
            memberships.Single(membership => membership.BranchId == command.BranchId).SetDefault(true);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Updated;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
