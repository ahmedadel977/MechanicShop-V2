using MechanicShop.Tests.Common.Auth;

using Xunit;

namespace MechanicShop.Domain.UnitTests.Auth;

public class RefreshTokenTests
{
    [Fact]
    public void CreateRefreshToken_ShouldSucceed_WithValidData()
    {
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid().ToString();
        var tokenHash = new string('b', 64);
        var familyId = Guid.NewGuid();
        var expiresOnUtc = DateTimeOffset.UtcNow.AddDays(7);

        var result = RefreshTokenFactory.CreateRefreshToken(
            id: id,
            tokenHash: tokenHash,
            userId: userId,
            familyId: familyId,
            expiresOnUtc: expiresOnUtc);

        Assert.True(result.IsSuccess);

        var token = result.Value;

        Assert.NotNull(token);
        Assert.Equal(tokenHash, token.TokenHash);
        Assert.Equal(familyId, token.FamilyId);
        Assert.Null(token.RevokedOnUtc);
        Assert.False(string.IsNullOrWhiteSpace(token.UserId));
        Assert.Equal(userId, token.UserId);
        Assert.True(token.ExpiresOnUtc > DateTimeOffset.UtcNow);
    }

    [Fact]
    public void CreateRefreshToken_ShouldFail_WhenIdEmpty()
    {
        var result = RefreshTokenFactory.CreateRefreshToken(id: Guid.Empty);

        Assert.True(result.IsError);

        Assert.Equal("RefreshToken_Id_Required", result.TopError.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRefreshToken_ShouldFail_WhenTokenHashInvalid(string? invalidToken)
    {
        var result = RefreshTokenFactory.CreateRefreshToken(tokenHash: invalidToken);

        Assert.True(result.IsError);

        Assert.Equal("RefreshToken_Token_Required", result.TopError.Code);
    }

    [Fact]
    public void Revoke_ShouldRecordRevocationAndReplacementHash()
    {
        var token = RefreshTokenFactory.CreateRefreshToken().Value;
        var revokedOnUtc = DateTimeOffset.UtcNow;
        var replacementHash = new string('c', 64);

        Assert.True(token.Revoke(revokedOnUtc, replacementHash));
        Assert.False(token.Revoke(revokedOnUtc.AddSeconds(1)));
        Assert.Equal(revokedOnUtc, token.RevokedOnUtc);
        Assert.Equal(replacementHash, token.ReplacedByTokenHash);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateRefreshToken_ShouldFail_WhenUserIdInvalid(string? invalidUserId)
    {
        var result = RefreshTokenFactory.CreateRefreshToken(userId: invalidUserId);

        Assert.True(result.IsError);

        Assert.Equal("RefreshToken_UserId_Required", result.TopError.Code);
    }

    [Fact]
    public void CreateRefreshToken_ShouldFail_WhenExpiresOnUtcIsInPast()
    {
        var result = RefreshTokenFactory.CreateRefreshToken(expiresOnUtc: DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.True(result.IsError);

        Assert.Equal("RefreshToken_Expiry_Invalid", result.TopError.Code);
    }
}
