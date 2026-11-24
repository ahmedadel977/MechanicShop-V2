using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common.Results;
using MediatR;

namespace MechanicShop.Application.Features.Identity.Commands.Logout;

public sealed class LogoutCommandHandler(ITokenProvider tokenProvider)
    : IRequestHandler<LogoutCommand, Result<Updated>>
{
    public async Task<Result<Updated>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        await tokenProvider.RevokeRefreshTokensAsync(request.UserId, cancellationToken);
        return Result.Updated;
    }
}
