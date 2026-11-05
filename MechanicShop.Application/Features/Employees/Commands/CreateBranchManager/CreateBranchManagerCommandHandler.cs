using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Employees.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MediatR;

namespace MechanicShop.Application.Features.Employees.Commands.CreateBranchManager;

public sealed class CreateBranchManagerCommandHandler(
    ICurrentEmployeeAccess currentEmployeeAccess,
    IEmployeeAccountService employeeAccountService)
    : IRequestHandler<CreateBranchManagerCommand, Result<EmployeeAccountDto>>
{
    public async Task<Result<EmployeeAccountDto>> Handle(
        CreateBranchManagerCommand command,
        CancellationToken cancellationToken)
    {
        var actor = await currentEmployeeAccess.GetCurrentAsync(cancellationToken);
        if (actor?.Role != Role.Manager || actor.AccessScope != AccessScope.Company)
        {
            return Error.Forbidden("Branch.CompanyScope.Required", "Only company-scoped managers can create branch managers.");
        }

        var accountResult = await employeeAccountService.CreateAsync(
            command.FirstName,
            command.LastName,
            command.Email,
            command.Password,
            Role.Manager,
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
            Role.Manager,
            AccessScope.Branch,
            command.BranchId);
    }
}
