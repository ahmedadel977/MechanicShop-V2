using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Identity.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Infrastructure.identity;

public sealed class IdentityService(
    UserManager<AppUser> userManager,
    IUserClaimsPrincipalFactory<AppUser> claimsFactory,
    IAuthorizationService authorizationService,
    IAppDbContext context) : IIdentityService
{
    private readonly UserManager<AppUser> _userManager = userManager;
    private readonly IUserClaimsPrincipalFactory<AppUser> _userClaimsPrincipalFactory = claimsFactory;
    private readonly IAuthorizationService _authorizationService = authorizationService;
    private readonly IAppDbContext _context = context;

    public async Task<Result<AppUserDto>> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
        {
            return Error.NotFound("User_Not_Found", $"User with email {UtilityService.MaskEmail(email)} not found");
        }

        if (!user.EmailConfirmed)
        {
            return Error.Conflict("Email_Not_Confirmed", $"email '{UtilityService.MaskEmail(email)}' not confirmed");
        }

        if (!await _userManager.CheckPasswordAsync(user, password))
        {
            return Error.Conflict("Invalid_Login_Attempt", "Email / Password are incorrect");
        }

        return await CreateCurrentUserDtoAsync(user, cancellationToken);
    }

    public async Task<Result<Updated>> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Error.NotFound("Auth.User.NotFound", "User not found.");
        }

        await using var transaction = await _context.BeginTransactionAsync(cancellationToken);
        var changeResult = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!changeResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.Validation(
                "Auth.PasswordChange.Failed",
                "The current password is invalid or the new password does not meet the password policy.");
        }

        var stampResult = await _userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Error.Failure("Auth.PasswordChange.Failed", "The password could not be changed.");
        }

        var revokedOnUtc = DateTimeOffset.UtcNow;
        await _context.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedOnUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedOnUtc, revokedOnUtc),
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Result.Updated;
    }

    public async Task<bool> AuthorizeAsync(string userId, string? policyName)
    {
        if (string.IsNullOrWhiteSpace(policyName))
        {
            return false;
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return false;
        }

        var principal = await _userClaimsPrincipalFactory.CreateAsync(user);
        var authorizationResult = await _authorizationService.AuthorizeAsync(principal, policyName);
        return authorizationResult.Succeeded;
    }

    public async Task<Result<AppUserDto>> GetUserByIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Error.NotFound("Auth.User.NotFound", "User not found.");
        }

        return await CreateCurrentUserDtoAsync(user, cancellationToken);
    }

    private async Task<Result<AppUserDto>> CreateCurrentUserDtoAsync(
        AppUser user,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(user.Id, out var employeeId) || employeeId == Guid.Empty)
        {
            return Error.Forbidden("Branch.Employee.Invalid");
        }

        var employee = await _context.Employees
            .Where(item => item.Id == employeeId)
            .Select(item => new { item.Role, item.AccessScope })
            .SingleOrDefaultAsync(cancellationToken);
        if (employee is null)
        {
            return Error.Forbidden("Branch.Employee.Invalid");
        }

        var identityRoles = await _userManager.GetRolesAsync(user);
        if (identityRoles.Count != 1
            || !string.Equals(identityRoles[0], employee.Role.ToString(), StringComparison.Ordinal))
        {
            return Error.Forbidden("Branch.Role.Mismatch");
        }

        if (!Enum.IsDefined(employee.Role)
            || !Enum.IsDefined(employee.AccessScope)
            || (employee.AccessScope == AccessScope.Company && employee.Role != Role.Manager))
        {
            return Error.Forbidden("Branch.AccessScope.Invalid");
        }

        Guid? branchId = null;
        if (employee.AccessScope == AccessScope.Branch)
        {
            branchId = await _context.EmployeeBranchMemberships
                .Where(membership => membership.EmployeeId == employeeId && membership.IsDefault)
                .Join(_context.Branches,
                    membership => membership.BranchId,
                    branch => branch.Id,
                    (_, branch) => new { branch.Id, branch.IsActive })
                .Where(branch => branch.IsActive)
                .Select(branch => (Guid?)branch.Id)
                .SingleOrDefaultAsync(cancellationToken);

            if (!branchId.HasValue)
            {
                return Error.Forbidden("Branch.DefaultBranch.Required");
            }
        }

        var claims = await _userManager.GetClaimsAsync(user);
        return new AppUserDto(
            user.Id,
            user.Email!,
            identityRoles,
            claims,
            branchId,
            employee.AccessScope);
    }

    public async Task<string?> GetUserNameAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        return user?.UserName;
    }

    public async Task<bool> IsInRoleAsync(string userId, string role)
    {
        var user = await _userManager.FindByIdAsync(userId);
        return user is not null && await _userManager.IsInRoleAsync(user, role);
    }
}
