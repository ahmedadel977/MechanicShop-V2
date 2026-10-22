using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Identity.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace MechanicShop.Infrastructure.identity
{
    public class IdentityService (UserManager<AppUser> userManager,
        IUserClaimsPrincipalFactory<AppUser> claimsFactory,
        IAuthorizationService authorizationService) : IIdentityService
    {
        private readonly UserManager<AppUser> _userManager = userManager;
        private readonly IUserClaimsPrincipalFactory<AppUser> _userClaimsPrincipalFactory = claimsFactory;
        private readonly IAuthorizationService _authorizationService = authorizationService;
        public async Task<Result<AppUserDto>> AuthenticateAsync(string email, string password) // This method authenticates a user based on their email and password (login functionality)
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

            return new AppUserDto(user.Id, user.Email!, await _userManager.GetRolesAsync(user), await _userManager.GetClaimsAsync(user));
            

        }

        public async Task<bool> AuthorizeAsync(string userId, string? policyName) // This method checks if a user is authorized based on a specific policy
        {
           var user = await _userManager.FindByIdAsync(userId);
            if 
                (user == null)
            {
                return false;
            }
            var principal = await _userClaimsPrincipalFactory.CreateAsync(user);
            var authorizationResult = await _authorizationService.AuthorizeAsync(principal, policyName);
            return authorizationResult.Succeeded;
        }

        public async Task<Result<AppUserDto>> GetUserByIdAsync(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId) ?? throw new InvalidOperationException(nameof(userId));

            var roles = await _userManager.GetRolesAsync(user);

            var claims = await _userManager.GetClaimsAsync(user);

            return new AppUserDto(user.Id, user.Email!, roles, claims);
        }
        public Task<string?> GetUserNameAsync(string userId)
        {
           var user = _userManager.FindByIdAsync(userId);
            return user.ContinueWith(t => t.Result?.UserName);
        }

        public async Task<bool> IsInRoleAsync(string userId, string role) // This method checks if a user is in a specific role
        {
           var user = await _userManager.FindByIdAsync(userId);
            return user != null && await _userManager.IsInRoleAsync(user, role);

        }
    }
}
