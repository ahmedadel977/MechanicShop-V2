using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Branches;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.Identity;
using MechanicShop.Infrastructure.Date;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Infrastructure.identity;

public sealed class EmployeeAccountService(
    AppDbContext context,
    UserManager<AppUser> userManager) : IEmployeeAccountService
{
    public async Task<Result<Guid>> CreateAsync(
        string firstName,
        string lastName,
        string email,
        string password,
        Role role,
        AccessScope accessScope,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var branchIsActive = await context.Branches.AnyAsync(
            branch => branch.Id == branchId && branch.IsActive,
            cancellationToken);
        if (!branchIsActive)
        {
            return Error.NotFound("Branch.NotFound", "The branch does not exist or is inactive.");
        }

        var employeeId = Guid.NewGuid();
        var employeeResult = Employee.Create(employeeId, firstName, lastName, role, accessScope);
        if (employeeResult.IsError)
        {
            return employeeResult.Errors;
        }

        var membershipResult = EmployeeBranchMembership.Create(employeeId, branchId, true);
        if (membershipResult.IsError)
        {
            return membershipResult.Errors;
        }

        try
        {
            var user = new AppUser
            {
                Id = employeeId.ToString(),
                Email = email,
                UserName = email,
                EmailConfirmed = true
            };

            var createUserResult = await userManager.CreateAsync(user, password);
            if (!createUserResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ToIdentityError(createUserResult);
            }

            var addRoleResult = await userManager.AddToRoleAsync(user, role.ToString());
            if (!addRoleResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ToIdentityError(addRoleResult);
            }

            await context.Employees.AddAsync(employeeResult.Value, cancellationToken);
            await context.EmployeeBranchMemberships.AddAsync(membershipResult.Value, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return employeeId;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static Error ToIdentityError(IdentityResult result)
    {
        var errors = result.Errors.ToList();
        if (errors.Any(error => error.Code.StartsWith("Duplicate", StringComparison.Ordinal)))
        {
            return Error.Conflict("Employee.Email.Duplicate", "An account with this email already exists.");
        }

        return Error.Validation(
            "Employee.Account.Invalid",
            string.Join(" ", errors.Select(error => error.Description)));
    }
}
