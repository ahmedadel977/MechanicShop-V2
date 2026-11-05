using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Branches.Commands.SetEmployeeAccessScope;

public sealed class SetEmployeeAccessScopeCommandHandler(
    IAppDbContext context,
    ICurrentEmployeeAccess currentEmployeeAccess)
    : IRequestHandler<SetEmployeeAccessScopeCommand, Result<Updated>>
{
    public async Task<Result<Updated>> Handle(
        SetEmployeeAccessScopeCommand command,
        CancellationToken cancellationToken)
    {
        var actor = await currentEmployeeAccess.GetCurrentAsync(cancellationToken);
        if (actor?.Role != Role.Manager || actor.AccessScope != AccessScope.Company)
        {
            return Error.Forbidden("Branch.CompanyScope.Required", "Only a company-scoped manager can change employee access scope.");
        }

        var employee = await context.Employees.SingleOrDefaultAsync(
            item => item.Id == command.EmployeeId,
            cancellationToken);
        if (employee is null)
        {
            return Error.NotFound("Employee.NotFound", "The employee does not exist.");
        }

        if (employee.Id == actor.EmployeeId
            && command.AccessScope == AccessScope.Company
            && employee.AccessScope != AccessScope.Company)
        {
            return Error.Forbidden("Branch.AccessScope.SelfElevation", "Users cannot elevate their own access scope.");
        }

        var result = employee.SetAccessScope(command.AccessScope);
        if (result.IsError)
        {
            return result.Errors;
        }

        await context.SaveChangesAsync(cancellationToken);
        return Result.Updated;
    }
}
