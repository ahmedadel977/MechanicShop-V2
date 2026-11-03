using MechanicShop.Application.Features.Identity.Queries.GenerateTokens;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.SubcutaneousTests.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;

namespace MechanicShop.Application.SubcutaneousTests.Features.Identity.Queries.GenerateTokens;

[Collection(WebAppFactoryCollection.CollectionName)]
public class GenerateTokenQueryHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenCredentialsDoNotMatchAUser_ShouldReturnAuthenticationError()
    {
        var result = await _mediator.Send(
            new GenerateTokenQuery($"missing-{Guid.NewGuid():N}@example.test", "incorrect-password"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Handle_WhenPasswordIsIncorrect_ShouldRejectExistingUser()
    {
        var result = await _mediator.Send(
            new GenerateTokenQuery("pm@localhost", "incorrect-password"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Handle_WhenSeededManagerCredentialsAreValid_ShouldIssueTokensAndPersistRefreshToken()
    {
        var result = await _mediator.Send(
            new GenerateTokenQuery("pm@localhost", "pm@localhost"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.Value.RefreshToken));
        Assert.True(await _context.RefreshTokens.AnyAsync(
            token => token.Token == result.Value.RefreshToken,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WhenLaborCredentialsAreValid_ShouldIncludeLaborRoleInAccessToken()
    {
        var result = await _mediator.Send(
            new GenerateTokenQuery("john.labor@localhost", "john.labor@localhost"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Value.AccessToken);
        Assert.Contains(token.Claims, claim => claim.Type == "role" && claim.Value == "Labor");
        Assert.DoesNotContain(token.Claims, claim => claim.Type == "role" && claim.Value == "Manager");
    }
}
