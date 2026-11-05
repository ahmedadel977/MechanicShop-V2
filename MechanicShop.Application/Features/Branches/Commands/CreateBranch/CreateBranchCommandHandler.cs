using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Branches.Dtos;
using MechanicShop.Domain.Branches;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Branches.Commands.CreateBranch;

public sealed class CreateBranchCommandHandler(
    IAppDbContext context,
    ICurrentEmployeeAccess currentEmployeeAccess)
    : IRequestHandler<CreateBranchCommand, Result<BranchDto>>
{
    public async Task<Result<BranchDto>> Handle(CreateBranchCommand command, CancellationToken cancellationToken)
    {
        var actor = await currentEmployeeAccess.GetCurrentAsync(cancellationToken);
        if (actor?.Role != Role.Manager || actor.AccessScope != AccessScope.Company)
        {
            return Error.Forbidden("Branch.CompanyScope.Required", "Only a company-scoped manager can create branches.");
        }

        var branchResult = Branch.Create(Guid.NewGuid(), command.Name, command.Code);
        if (branchResult.IsError)
        {
            return branchResult.Errors;
        }

        var branch = branchResult.Value;
        await context.Branches.AddAsync(branch, cancellationToken);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Error.Conflict("Branch.Code.Duplicate", "The branch code is already in use.");
        }

        return new BranchDto(branch.Id, branch.Name, branch.Code, branch.IsActive);
    }
}
