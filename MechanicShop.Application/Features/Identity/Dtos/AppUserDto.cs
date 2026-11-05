using System.Security.Claims;
using MechanicShop.Domain.Identity;

namespace MechanicShop.Application.Features.Identity.Dtos;

public sealed record AppUserDto(string UserId, string Email, IList<string> Roles, IList<Claim> Claims, Guid? BranchId, AccessScope AccessScope);
