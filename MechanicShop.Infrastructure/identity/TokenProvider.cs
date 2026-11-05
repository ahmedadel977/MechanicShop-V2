using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Identity;
using MechanicShop.Application.Features.Identity.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using MechanicShop.Infrastructure.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace MechanicShop.Infrastructure.identity;

public sealed class TokenProvider(IAppDbContext context, IOptions<JwtSettings> options) : ITokenProvider
{
    private readonly IAppDbContext _context = context;
    private readonly JwtSettings _settings = options.Value;

    public async Task<Result<TokenResponse>> GenerateJwtTokenAsync(AppUserDto user, CancellationToken ct = default)
    {
        var familyId = Guid.NewGuid();
        var (token, rawRefreshToken) = CreateRefreshToken(user.UserId!, familyId);
        if (token.IsError)
        {
            return token.Errors;
        }

        var accessToken = CreateAccessToken(user, out var expiresOnUtc);
        _context.RefreshTokens.Add(token.Value);
        await _context.SaveChangesAsync(ct);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            ExpiresOnUtc = expiresOnUtc
        };
    }

    public async Task<Result<TokenResponse>> RotateRefreshTokenAsync(
        AppUserDto user,
        string refreshToken,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken) || string.IsNullOrWhiteSpace(user.UserId))
        {
            return ApplicationErrors.RefreshTokenExpired;
        }

        var now = DateTimeOffset.UtcNow;
        var tokenHash = HashToken(refreshToken);
        var current = await _context.RefreshTokens.SingleOrDefaultAsync(
            token => token.TokenHash == tokenHash && token.UserId == user.UserId,
            ct);

        if (current is null)
        {
            return ApplicationErrors.RefreshTokenExpired;
        }

        if (current.RevokedOnUtc.HasValue)
        {
            await RevokeFamilyAsync(current.FamilyId, now, ct);
            return ApplicationErrors.RefreshTokenReuseDetected;
        }

        if (current.ExpiresOnUtc <= now)
        {
            return ApplicationErrors.RefreshTokenExpired;
        }

        var replacementRawToken = GenerateRefreshToken();
        var replacementHash = HashToken(replacementRawToken);
        var replacement = RefreshToken.Create(
            Guid.NewGuid(),
            replacementHash,
            user.UserId,
            current.FamilyId,
            now.AddDays(7));
        if (replacement.IsError)
        {
            return replacement.Errors;
        }

        var accessToken = CreateAccessToken(user, out var expiresOnUtc);
        await using var transaction = await _context.BeginTransactionAsync(ct);
        var consumedCount = await _context.RefreshTokens
            .Where(token => token.Id == current.Id
                && token.RevokedOnUtc == null
                && token.ExpiresOnUtc > now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(token => token.RevokedOnUtc, now)
                .SetProperty(token => token.ReplacedByTokenHash, replacementHash), ct);

        if (consumedCount != 1)
        {
            await transaction.RollbackAsync(ct);
            await RevokeFamilyAsync(current.FamilyId, now, ct);
            return ApplicationErrors.RefreshTokenReuseDetected;
        }

        _context.RefreshTokens.Add(replacement.Value);
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = replacementRawToken,
            ExpiresOnUtc = expiresOnUtc
        };
    }

    public async Task RevokeRefreshTokensAsync(string userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        await _context.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedOnUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedOnUtc, now), ct);
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 16_384)
        {
            return null;
        }

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ValidIssuer = _settings.Issuer,
            ValidAudience = _settings.Audience,
            IssuerSigningKey = CreateSigningKey(),
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);
            if (securityToken is not JwtSecurityToken jwt
                || jwt.ValidTo == DateTime.MinValue
                || jwt.ValidTo > DateTime.UtcNow
                || !string.Equals(jwt.Header.Alg, SecurityAlgorithms.HmacSha256, StringComparison.Ordinal))
            {
                return null;
            }

            return principal;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static (Result<RefreshToken> Token, string RawToken) CreateRefreshToken(string userId, Guid familyId)
    {
        var rawToken = GenerateRefreshToken();
        var token = RefreshToken.Create(
            Guid.NewGuid(),
            HashToken(rawToken),
            userId,
            familyId,
            DateTimeOffset.UtcNow.AddDays(7));
        return (token, rawToken);
    }

    private string CreateAccessToken(AppUserDto user, out DateTime expiresOnUtc)
    {
        expiresOnUtc = DateTime.UtcNow.AddMinutes(_settings.TokenExpirationInMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId!),
            new("access_scope", user.AccessScope.ToString())
        };

        if (user.BranchId.HasValue)
        {
            claims.Add(new Claim("branch_id", user.BranchId.Value.ToString()));
        }

        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresOnUtc,
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            SigningCredentials = new SigningCredentials(CreateSigningKey(), SecurityAlgorithms.HmacSha256)
        };

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityTokenHandler().CreateToken(descriptor));
    }

    private SymmetricSecurityKey CreateSigningKey() =>
        new(Encoding.UTF8.GetBytes(_settings.Secret));

    private static string GenerateRefreshToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken ct)
    {
        await _context.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedOnUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedOnUtc, now), ct);
    }
}
