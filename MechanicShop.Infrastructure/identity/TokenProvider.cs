using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Identity;
using MechanicShop.Application.Features.Identity.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace MechanicShop.Infrastructure.identity
{
    public class TokenProvider (IAppDbContext context , IConfiguration configuration ) : ITokenProvider
    {
        private IAppDbContext _Context { get; } = context;
        private IConfiguration _configuration { get; } = configuration;

        public async Task<Result<TokenResponse>> GenerateJwtTokenAsync(AppUserDto user, CancellationToken ct = default)
        {
            var tokenResult = await CreateAsync(user, ct);

            if (tokenResult.IsError)
            {
                return tokenResult.Errors;
            }

            return tokenResult.Value;
        }

        public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var issuer = jwtSettings["Issuer"]!;
            var audience = jwtSettings["Audience"]!;
            var key = jwtSettings["Secret"]!;
            var tokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false, // We want to get the principal from an expired token
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                 ClockSkew = TimeSpan.Zero
            };
            var tokenHandler = new JwtSecurityTokenHandler();
            try
            {
                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken securityToken);
                if (securityToken is not JwtSecurityToken jwtSecurityToken || !jwtSecurityToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new SecurityTokenException("Invalid token");
                }
                return principal;
            }
            catch
            {
                return null;
            }


        }
        private async Task<Result<TokenResponse>> CreateAsync(AppUserDto user, CancellationToken ct = default)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");

            var issuer = jwtSettings["Issuer"]!;
            var audience = jwtSettings["Audience"]!;
            var key = jwtSettings["Secret"]!;

            var expires = DateTime.UtcNow.AddMinutes(int.Parse(jwtSettings["TokenExpirationInMinutes"]!));

            var claims = new List<Claim>
        {
            new (JwtRegisteredClaimNames.Sub, user.UserId!),
            new (JwtRegisteredClaimNames.Email, user.Email!),

            new ("access_scope", user.AccessScope.ToString()),
        };

            if (user.BranchId.HasValue)
            {
                claims.Add(new Claim("branch_id", user.BranchId.Value.ToString()));
            }

            foreach (var role in user.Roles)
            {
                claims.Add(new(ClaimTypes.Role, role));
            } 
            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expires,
                Issuer = issuer,
                Audience = audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256Signature)
            };
            var tokenHandler = new JwtSecurityTokenHandler();
            var securityToken = tokenHandler.CreateToken(descriptor);
            var oldRefreshToken = await _Context.RefreshTokens.Where(rt => rt.UserId == user.UserId).
                ExecuteDeleteAsync(cancellationToken: ct);
           
            var refreshTokenResult = RefreshToken.Create(
             Guid.NewGuid(),
             GenerateRefreshToken(),
             user.UserId,
             DateTime.UtcNow.AddDays(7));
            if (refreshTokenResult.IsError)
            {
                return refreshTokenResult.Errors;
            }
            var refreshToken = refreshTokenResult.Value;
            _Context.RefreshTokens.Add(refreshToken);
            await _Context.SaveChangesAsync(ct);
            return new TokenResponse
            {
                AccessToken = tokenHandler.WriteToken(securityToken),
                RefreshToken = refreshToken.Token,
                ExpiresOnUtc = expires
            };


        }
           
        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomNumber);
                return Convert.ToBase64String(randomNumber);
            }
        }
    }
}
