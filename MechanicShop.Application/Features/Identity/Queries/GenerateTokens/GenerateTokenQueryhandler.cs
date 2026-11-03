using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;

using MediatR;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.Features.Identity.Queries.GenerateTokens;

public class GenerateTokenQueryhandler (IIdentityService identityService, ITokenProvider tokenProvider,ILogger<GenerateTokenQueryhandler> logger    ) : IRequestHandler<GenerateTokenQuery, Result<TokenResponse>>
{
    private readonly ITokenProvider _tokenProvider = tokenProvider;
    private readonly IIdentityService _identityService = identityService;
    private readonly ILogger<GenerateTokenQueryhandler> _logger = logger;


    public async Task<Result<TokenResponse>> Handle(GenerateTokenQuery context, CancellationToken cancellationToken)
    {
      var userResponse  = await  _identityService.AuthenticateAsync(context.Email, context.Password);
        if (userResponse.IsError)
        {
            _logger.LogWarning("Authentication failed for user {Email}. Errors: {Errors}", context.Email, userResponse.Errors);
            return userResponse.Errors;
        }

        var token = await _tokenProvider.GenerateJwtTokenAsync(userResponse.Value);
        if (token.IsError)
        {
            _logger.LogError("Generate token error occurred: {ErrorDescription}", token.TopError.Description);

            return token.Errors;
        }

        return token.Value;
    }
}
