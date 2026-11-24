using MechanicShop.Application.Features.Identity.Queries.RefreshTokens;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Identity.Dtos;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace MechanicShop.Application.SubcutaneousTests.Features.Identity.Queries.RefreshTokens;

[Collection(WebAppFactoryCollection.CollectionName)]
public class RefreshTokenQueryHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenExpiredAccessTokenIsMalformed_ShouldReturnError()
    {
        var result = await _mediator.Send(
            new RefreshTokenQuery("irrelevant-refresh-token", "not-a-jwt"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal("Auth.ExpiredAccessToken.Invalid", result.Errors[0].Code);
    }

    [Fact]
    public async Task Handle_WhenRefreshTokenAndExpiredAccessTokenBelongToUser_ShouldIssueAndPersistReplacement()
    {
        var user = await GetManagerAsync();
        var refreshToken = await AddRefreshTokenAsync(user.UserId);
        var expiredAccessToken = CreateAccessToken(user.UserId, DateTime.UtcNow.AddMinutes(-5));

        var result = await _mediator.Send(
            new RefreshTokenQuery(refreshToken, expiredAccessToken),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.Value.RefreshToken));
        Assert.NotEqual(refreshToken, result.Value.RefreshToken);
        var previousToken = await _context.RefreshTokens
            .AsNoTracking()
            .SingleAsync(
                token => token.TokenHash == HashToken(refreshToken),
                TestContext.Current.CancellationToken);
        Assert.NotNull(previousToken.RevokedOnUtc);
        Assert.Equal(HashToken(result.Value.RefreshToken), previousToken.ReplacedByTokenHash);
        Assert.True(await _context.RefreshTokens.AnyAsync(
            token => token.TokenHash == HashToken(result.Value.RefreshToken)
                && token.UserId == user.UserId
                && token.FamilyId == previousToken.FamilyId,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WhenAccessTokenHasNotExpired_ShouldRejectRefreshRequest()
    {
        var user = await GetManagerAsync();
        var refreshToken = await AddRefreshTokenAsync(user.UserId);
        var validAccessToken = CreateAccessToken(user.UserId, DateTime.UtcNow.AddMinutes(15));

        var result = await _mediator.Send(
            new RefreshTokenQuery(refreshToken, validAccessToken),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal("Auth.ExpiredAccessToken.Invalid", result.Errors[0].Code);
    }

    [Fact]
    public async Task Handle_WhenRotatedRefreshTokenIsReused_ShouldRevokeItsFamily()
    {
        var user = await GetManagerAsync();
        var refreshToken = await AddRefreshTokenAsync(user.UserId);
        var expiredAccessToken = CreateAccessToken(user.UserId, DateTime.UtcNow.AddMinutes(-5));
        var familyId = await _context.RefreshTokens
            .Where(token => token.TokenHash == HashToken(refreshToken))
            .Select(token => token.FamilyId)
            .SingleAsync(TestContext.Current.CancellationToken);

        var firstRotation = await _mediator.Send(
            new RefreshTokenQuery(refreshToken, expiredAccessToken),
            TestContext.Current.CancellationToken);
        Assert.True(firstRotation.IsSuccess);

        var replay = await _mediator.Send(
            new RefreshTokenQuery(refreshToken, expiredAccessToken),
            TestContext.Current.CancellationToken);

        Assert.True(replay.IsError);
        Assert.Equal("Auth.RefreshToken.ReuseDetected", replay.Errors[0].Code);
        Assert.All(
            await _context.RefreshTokens
                .AsNoTracking()
                .Where(token => token.FamilyId == familyId)
                .ToListAsync(TestContext.Current.CancellationToken),
            token => Assert.NotNull(token.RevokedOnUtc));
    }

    [Fact]
    public async Task Handle_WhenRefreshTokenHasExpired_ShouldReturnExpiredRefreshTokenError()
    {
        var user = await GetManagerAsync();
        var refreshToken = await AddRefreshTokenAsync(user.UserId);
        await _context.RefreshTokens
            .Where(token => token.TokenHash == HashToken(refreshToken))
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    token => token.ExpiresOnUtc,
                    DateTimeOffset.UtcNow.AddMinutes(-1)),
                TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new RefreshTokenQuery(refreshToken, CreateAccessToken(user.UserId, DateTime.UtcNow.AddMinutes(-5))),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal("Auth.RefreshToken.Expired", result.Errors[0].Code);
    }

    [Fact]
    public async Task Handle_WhenRefreshTokenIsUnknown_ShouldReturnExpiredRefreshTokenError()
    {
        var user = await GetManagerAsync();

        var result = await _mediator.Send(
            new RefreshTokenQuery(Guid.NewGuid().ToString("N"), CreateAccessToken(user.UserId, DateTime.UtcNow.AddMinutes(-5))),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal("Auth.RefreshToken.Expired", result.Errors[0].Code);
    }

    [Fact]
    public async Task Handle_WhenRefreshTokenBelongsToAnotherUser_ShouldRejectIt()
    {
        var user = await GetManagerAsync();
        var refreshToken = await AddRefreshTokenAsync(Guid.NewGuid().ToString());

        var result = await _mediator.Send(
            new RefreshTokenQuery(refreshToken, CreateAccessToken(user.UserId, DateTime.UtcNow.AddMinutes(-5))),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal("Auth.RefreshToken.Expired", result.Errors[0].Code);
    }

    [Fact]
    public async Task Handle_WhenExpiredAccessTokenHasNoUserIdClaim_ShouldReturnClaimError()
    {
        var result = await _mediator.Send(
            new RefreshTokenQuery("unused-refresh-token", CreateAccessToken(null, DateTime.UtcNow.AddMinutes(-5))),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal("Auth.UserIdClaim.Invalid", result.Errors[0].Code);
    }

    [Fact]
    public async Task Handle_WhenAccessTokenReferencesMissingUser_ShouldReturnNotFound()
    {
        var missingUserId = Guid.NewGuid().ToString();
        var refreshToken = await AddRefreshTokenAsync(missingUserId);
        var result = await _mediator.Send(
            new RefreshTokenQuery(
                refreshToken,
                CreateAccessToken(missingUserId, DateTime.UtcNow.AddMinutes(-5))),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal("Auth.User.NotFound", result.Errors[0].Code);
    }

    [Fact]
    public async Task Handle_WhenCompanyManagerTokenHasNoBranchClaim_ShouldRefreshWithoutAddingBranchClaim()
    {
        var user = await GetManagerAsync();
        var managerId = Guid.Parse(user.UserId);
        var memberships = await _context.EmployeeBranchMemberships
            .Where(membership => membership.EmployeeId == managerId)
            .ToListAsync(TestContext.Current.CancellationToken);
        _context.EmployeeBranchMemberships.RemoveRange(memberships);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        try
        {
            var refreshToken = await AddRefreshTokenAsync(user.UserId);
            var expiredAccessToken = CreateAccessToken(
                user.UserId,
                DateTime.UtcNow.AddMinutes(-5),
                branchId: null);

            var result = await _mediator.Send(
                new RefreshTokenQuery(refreshToken, expiredAccessToken),
                TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            var principal = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.AccessToken);
            Assert.Equal("Company", principal.Claims.Single(claim => claim.Type == "access_scope").Value);
            Assert.DoesNotContain(principal.Claims, claim => claim.Type == "branch_id");
        }
        finally
        {
            _context.EmployeeBranchMemberships.AddRange(memberships);
            await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private async Task<AppUserDto> GetManagerAsync()
    {
        using var scope = factory.Services.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();
        var result = await identityService.GetUserByIdAsync("19a59129-6c20-417a-834d-11a208d32d96");
        Assert.False(result.IsError);
        return result.Value;
    }

    private async Task<string> AddRefreshTokenAsync(string userId)
    {
        var token = $"refresh-{Guid.NewGuid():N}";
        var entity = RefreshToken.Create(
            Guid.NewGuid(),
            HashToken(token),
            userId,
            Guid.NewGuid(),
            DateTimeOffset.UtcNow.AddDays(1)).Value;
        await _context.RefreshTokens.AddAsync(entity, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return token;
    }

    private static string HashToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private string CreateAccessToken(string? userId, DateTime expiresAtUtc, Guid? branchId = null)
    {
        var configuration = factory.Services.GetRequiredService<IConfiguration>().GetSection("JwtSettings");
        var claims = userId is null
            ? Array.Empty<Claim>()
            : branchId.HasValue
                ? [
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim("branch_id", branchId.Value.ToString())
                  ]
                : [new Claim(ClaimTypes.NameIdentifier, userId)];
        var token = new JwtSecurityToken(
            issuer: configuration["Issuer"],
            audience: configuration["Audience"],
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Secret"]!)),
                SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
