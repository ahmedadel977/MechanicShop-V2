using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Employees.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Employees.Commands.CreateLabor;

public sealed class CreateLaborCommandHandler(
    IAppDbContext context,
    ICurrentEmployeeAccess currentEmployeeAccess,
    IEmployeeAccountService employeeAccountService)
    : IRequestHandler<CreateLaborCommand, Result<EmployeeAccountDto>>
{
    public async Task<Result<EmployeeAccountDto>> Handle(
        CreateLaborCommand command,
        CancellationToken cancellationToken)
    {
        var actor = await currentEmployeeAccess.GetCurrentAsync(cancellationToken);
        if (actor?.Role != Role.Manager)
        {
            return Error.Forbidden("Employee.Account.Create.Forbidden", "Only managers can create labor accounts.");
        }

        if (actor.AccessScope == AccessScope.Branch
            && !await context.EmployeeBranchMemberships.AnyAsync(
                membership => membership.EmployeeId == actor.EmployeeId
                    && membership.BranchId == command.BranchId,
                cancellationToken))
        {
            return Error.Forbidden("Branch.Access.Denied", "You are not authorized to create employees for this branch.");
        }

        if (!Enum.IsDefined(actor.AccessScope))
        {
            return Error.Forbidden("Branch.AccessScope.Invalid", "The current employee access scope is invalid.");
        }

        var accountResult = await employeeAccountService.CreateAsync(
            command.FirstName,
            command.LastName,
            command.Email,
            command.Password,
            Role.Labor,
            AccessScope.Branch,
            command.BranchId,
            cancellationToken);
        if (accountResult.IsError)
        {
            return accountResult.Errors;
        }

        return new EmployeeAccountDto(
            accountResult.Value,
            command.Email,
            Role.Labor,
            AccessScope.Branch,
            command.BranchId);
    }
}
