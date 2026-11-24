using MechanicShop.Domain.Common;
using MechanicShop.Domain.Common.Results;

namespace MechanicShop.Domain.Identity;

public sealed class RefreshToken : AuditableEntity
{
    public string TokenHash { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public Guid FamilyId { get; private set; }
    public DateTimeOffset ExpiresOnUtc { get; }
    public DateTimeOffset? RevokedOnUtc { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }

    private RefreshToken()
    { }

    private RefreshToken(Guid id, string tokenHash, string userId, Guid familyId, DateTimeOffset expiresOnUtc)
        : base(id)
    {
        TokenHash = tokenHash;
        UserId = userId;
        FamilyId = familyId;
        ExpiresOnUtc = expiresOnUtc;
    }

    public static Result<RefreshToken> Create(
        Guid id,
        string? tokenHash,
        string? userId,
        Guid familyId,
        DateTimeOffset expiresOnUtc)
    {
        if (id == Guid.Empty)
        {
            return RefreshTokenErrors.IdRequired;
        }

        if (string.IsNullOrWhiteSpace(tokenHash)
            || tokenHash.Length != 64
            || !tokenHash.All(Uri.IsHexDigit))
        {
            return RefreshTokenErrors.TokenRequired;
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return RefreshTokenErrors.UserIdRequired;
        }

        if (familyId == Guid.Empty)
        {
            return RefreshTokenErrors.FamilyIdRequired;
        }

        if (expiresOnUtc <= DateTimeOffset.UtcNow)
        {
            return RefreshTokenErrors.ExpiryInvalid;
        }

        return new RefreshToken(id, tokenHash, userId, familyId, expiresOnUtc);
    }

    public bool Revoke(DateTimeOffset revokedOnUtc, string? replacedByTokenHash = null)
    {
        if (RevokedOnUtc.HasValue)
        {
            return false;
        }

        RevokedOnUtc = revokedOnUtc;
        ReplacedByTokenHash = replacedByTokenHash;
        return true;
    }
}
