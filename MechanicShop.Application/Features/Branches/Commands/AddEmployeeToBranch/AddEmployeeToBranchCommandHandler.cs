using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Branches;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Branches.Commands.AddEmployeeToBranch;

public sealed class AddEmployeeToBranchCommandHandler(
    IAppDbContext context,
    ICurrentEmployeeAccess currentEmployeeAccess)
    : IRequestHandler<AddEmployeeToBranchCommand, Result<Created>>
{
    public async Task<Result<Created>> Handle(
        AddEmployeeToBranchCommand command,
        CancellationToken cancellationToken)
    {
        var actor = await currentEmployeeAccess.GetCurrentAsync(cancellationToken);
        var canManageBranch = actor?.Role == Role.Manager
            && (actor.AccessScope == AccessScope.Company
                || (actor.AccessScope == AccessScope.Branch
                    && await context.EmployeeBranchMemberships.AnyAsync(
                        membership => membership.EmployeeId == actor.EmployeeId
                            && membership.BranchId == command.BranchId,
                        cancellationToken)));
        if (!canManageBranch)
        {
            return Error.Forbidden("Branch.Access.Denied", "You are not allowed to manage memberships for the requested branch.");
        }

        var branchIsActive = await context.Branches.AnyAsync(
            branch => branch.Id == command.BranchId && branch.IsActive,
            cancellationToken);
        if (!branchIsActive)
        {
            return Error.NotFound("Branch.NotFound", "The branch does not exist or is inactive.");
        }

        if (!await context.Employees.AnyAsync(employee => employee.Id == command.EmployeeId, cancellationToken))
        {
            return Error.NotFound("Employee.NotFound", "The employee does not exist.");
        }

        if (await context.EmployeeBranchMemberships.AnyAsync(
                membership => membership.EmployeeId == command.EmployeeId
                    && membership.BranchId == command.BranchId,
                cancellationToken))
        {
            return Error.Conflict("Branch.Membership.Duplicate", "The employee is already a member of this branch.");
        }

        var isDefault = !await context.EmployeeBranchMemberships.AnyAsync(
            membership => membership.EmployeeId == command.EmployeeId && membership.IsDefault,
            cancellationToken);
        var membershipResult = EmployeeBranchMembership.Create(command.EmployeeId, command.BranchId, isDefault);
        if (membershipResult.IsError)
        {
            return membershipResult.Errors;
        }

        await context.EmployeeBranchMemberships.AddAsync(membershipResult.Value, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return Result.Created;
    }
}
