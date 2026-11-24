using System.Security.Claims;

using MechanicShop.Application.Features.Identity;
using MechanicShop.Application.Features.Identity.Dtos;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Application.Common.Interfaces;

public interface ITokenProvider
{
    Task<Result<TokenResponse>> GenerateJwtTokenAsync(AppUserDto user, CancellationToken ct = default);

    Task<Result<TokenResponse>> RotateRefreshTokenAsync(
        AppUserDto user,
        string refreshToken,
        CancellationToken ct = default);

    Task RevokeRefreshTokensAsync(string userId, CancellationToken ct = default);

    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
