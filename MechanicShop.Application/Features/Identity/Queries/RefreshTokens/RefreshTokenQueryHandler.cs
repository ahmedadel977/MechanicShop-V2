using System.Security.Claims;
using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Identity.Queries.RefreshTokens;

public sealed class RefreshTokenQueryHandler(
    ILogger<RefreshTokenQueryHandler> logger,
    IIdentityService identityService,
    IAppDbContext context,
    ITokenProvider tokenProvider)
    : IRequestHandler<RefreshTokenQuery, Result<TokenResponse>>
{
    private readonly ILogger<RefreshTokenQueryHandler> _logger = logger;
    private readonly IIdentityService _identityService = identityService;
    private readonly IAppDbContext _context = context;
    private readonly ITokenProvider _tokenProvider = tokenProvider;

    public async Task<Result<TokenResponse>> Handle(RefreshTokenQuery request, CancellationToken cancellationToken)
    {
        var principal = _tokenProvider.GetPrincipalFromExpiredToken(request.ExpiredAccessToken);
        if (principal is null)
        {
            _logger.LogWarning("Invalid expired access token.");
            return ApplicationErrors.ExpiredAccessTokenInvalid;
        }

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("UserId claim is missing or invalid in the expired access token.");
            return ApplicationErrors.UserIdClaimInvalid;
        }

        var refreshToken = await _context.RefreshTokens
            .SingleOrDefaultAsync(
                token => token.Token == request.RefreshToken && token.UserId == userId,
                cancellationToken);
        if (refreshToken is null || refreshToken.ExpiresOnUtc <= DateTimeOffset.UtcNow)
        {
            _logger.LogWarning("Refresh token is invalid, expired, or does not belong to UserId {UserId}.", userId);
            return ApplicationErrors.RefreshTokenExpired;
        }

        var userResult = await _identityService.GetUserByIdAsync(userId, cancellationToken);
        if (userResult.IsError)
        {
            _logger.LogWarning("Could not reload current identity data for UserId {UserId}.", userId);
            return userResult.Errors;
        }

        var tokenResult = await _tokenProvider.GenerateJwtTokenAsync(userResult.Value, cancellationToken);
        if (tokenResult.IsError)
        {
            _logger.LogError("Token generation failed for UserId {UserId}.", userId);
            return tokenResult.Errors;
        }

        return tokenResult.Value;
    }
}
