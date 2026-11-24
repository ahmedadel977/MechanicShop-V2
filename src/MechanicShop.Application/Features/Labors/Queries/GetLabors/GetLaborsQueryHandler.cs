using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Labors.Dtos;
using MechanicShop.Application.Features.Labors.Mappers;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;

using MediatR;

using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.Features.Labors.Queries.GetLabors;

public class GetLaborsQueryHandler(IAppDbContext context, ICurrentBranchContext currentBranchContext)
    : IRequestHandler<GetLaborsQuery, Result<List<LaborDto>>>
{
    private readonly IAppDbContext _context = context;
    private readonly ICurrentBranchContext _currentBranchContext = currentBranchContext;

    public async Task<Result<List<LaborDto>>> Handle(GetLaborsQuery query, CancellationToken ct)
    {
        if (_currentBranchContext.IsCompanyWide)
        {
            var companyLabors = await _context.Employees.AsNoTracking()
                .Where(employee => employee.Role == Role.Labor)
                .ToListAsync(ct);

            return companyLabors.ToDtos();
        }

        if (!_currentBranchContext.BranchId.HasValue)
        {
            return Error.Unauthorized("Branch.Active.Required", "An active branch is required to retrieve branch laborers.");
        }

        var branchId = _currentBranchContext.BranchId.Value;
        var labors = await _context.Employees.AsNoTracking()
            .Where(employee => employee.Role == Role.Labor
                && _context.EmployeeBranchMemberships.Any(membership =>
                    membership.EmployeeId == employee.Id && membership.BranchId == branchId))
            .ToListAsync(ct);

        return labors.ToDtos();
    }
}
