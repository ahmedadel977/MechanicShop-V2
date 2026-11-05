using System.Security.Claims;
using MechanicShop.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace MechanicShop.Infrastructure.identity;

public sealed class CurrentBranchContext(IHttpContextAccessor httpContextAccessor) : ICurrentBranchContext
{
    public Guid? EmployeeId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var employeeId) && employeeId != Guid.Empty
                ? employeeId
                : null;
        }
    }

    public bool IsCompanyWide => string.Equals(
        httpContextAccessor.HttpContext?.User.FindFirstValue("access_scope"),
        "Company",
        StringComparison.Ordinal);

    public bool IsLabor => httpContextAccessor.HttpContext?.User.IsInRole("Labor") == true;

    public Guid? BranchId
    {
        get
        {
            var value = httpContextAccessor.HttpContext?.User.FindFirstValue("branch_id");
            return Guid.TryParse(value, out var branchId) && branchId != Guid.Empty
                ? branchId
                : null;
        }
    }
}
