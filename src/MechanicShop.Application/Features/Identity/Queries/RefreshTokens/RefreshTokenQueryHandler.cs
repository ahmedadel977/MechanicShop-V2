using System.Security.Claims;
using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Identity.Queries.RefreshTokens;

public sealed class RefreshTokenQueryHandler(
    ILogger<RefreshTokenQueryHandler> logger,
    IIdentityService identityService,
    ITokenProvider tokenProvider)
    : IRequestHandler<RefreshTokenQuery, Result<TokenResponse>>
{
    private readonly ILogger<RefreshTokenQueryHandler> _logger = logger;
    private readonly IIdentityService _identityService = identityService;
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

        var userResult = await _identityService.GetUserByIdAsync(userId, cancellationToken);
        if (userResult.IsError)
        {
            _logger.LogWarning("Could not reload current identity data for UserId {UserId}.", userId);
            return userResult.Errors;
        }

        var tokenResult = await _tokenProvider.RotateRefreshTokenAsync(
            userResult.Value,
            request.RefreshToken,
            cancellationToken);
        if (tokenResult.IsError)
        {
            _logger.LogError("Token generation failed for UserId {UserId}.", userId);
            return tokenResult.Errors;
        }

        return tokenResult.Value;
    }
}
