using MechanicShop.Application.Features.Identity.Dtos;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Application.Common.Interfaces;

public interface IIdentityService
{
    Task<bool> IsInRoleAsync(string userId, string role);

    Task<bool> AuthorizeAsync(string userId, string? policyName);

    Task<Result<AppUserDto>> AuthenticateAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<Result<AppUserDto>> GetUserByIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<Result<Updated>> ChangePasswordAsync(
        string userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task<string?> GetUserNameAsync(string userId);
}
