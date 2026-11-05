using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Identity;

namespace MechanicShop.Tests.Common.Auth;

public static class RefreshTokenFactory
{
    public static Result<RefreshToken> CreateRefreshToken(
        Guid? id = null,
        string? tokenHash = null,
        string? userId = null,
        Guid? familyId = null,
        DateTimeOffset? expiresOnUtc = null)
    {
        return RefreshToken.Create(
            id ?? Guid.NewGuid(),
            tokenHash ?? new string('a', 64),
            userId ?? Guid.NewGuid().ToString(),
            familyId ?? Guid.NewGuid(),
            expiresOnUtc ?? DateTime.UtcNow.AddDays(7));
    }
}
