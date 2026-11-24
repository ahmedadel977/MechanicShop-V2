using System.Security.Claims;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Infrastructure.identity.policies;

public sealed class ActiveBranchAccessHandler(
    IAppDbContext context,
    IHttpContextAccessor httpContextAccessor,
    ICurrentEmployeeAccess currentEmployeeAccess) : AuthorizationHandler<ActiveBranchAccessRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext authorizationContext,
        ActiveBranchAccessRequirement requirement)
    {
        var principal = authorizationContext.User;
        if (principal.Identity?.IsAuthenticated != true
            || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var employeeId)
            || employeeId == Guid.Empty)
        {
            authorizationContext.Fail();
            return;
        }

        var cancellationToken = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        var employee = await currentEmployeeAccess.GetCurrentAsync(cancellationToken);
        if (employee is null || employee.EmployeeId != employeeId)
        {
            authorizationContext.Fail();
            return;
        }

        var roleClaims = principal.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
        if (roleClaims.Length != 1
            || !string.Equals(roleClaims[0], employee.Role.ToString(), StringComparison.Ordinal)
            || !string.Equals(
                principal.FindFirstValue("access_scope"),
                employee.AccessScope.ToString(),
                StringComparison.Ordinal))
        {
            authorizationContext.Fail();
            return;
        }

        if (employee.Role == Role.Manager && employee.AccessScope == AccessScope.Company)
        {
            authorizationContext.Succeed(requirement);
            return;
        }

        if (!Enum.IsDefined(employee.Role)
            || !Enum.IsDefined(employee.AccessScope)
            || employee.AccessScope != AccessScope.Branch
            || !Guid.TryParse(principal.FindFirstValue("branch_id"), out var branchId)
            || branchId == Guid.Empty)
        {
            authorizationContext.Fail();
            return;
        }

        var hasAccess = await context.EmployeeBranchMemberships
            .Where(membership => membership.EmployeeId == employeeId && membership.BranchId == branchId)
            .Join(context.Branches,
                membership => membership.BranchId,
                branch => branch.Id,
                (_, branch) => branch.IsActive)
            .AnyAsync(isActive => isActive, cancellationToken);

        if (hasAccess)
        {
            authorizationContext.Succeed(requirement);
            return;
        }

        authorizationContext.Fail();
    }
}
