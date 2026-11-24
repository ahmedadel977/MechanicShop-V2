using MechanicShop.Application.Features.Identity.Queries.GetUserInfo;
using MechanicShop.Application.SubcutaneousTests.Common;
using MediatR;
using MechanicShop.Infrastructure.identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace MechanicShop.Application.SubcutaneousTests.Features.Identity.Queries.GetUserInfo;

[Collection(WebAppFactoryCollection.CollectionName)]
public class GetUserByIdQueryHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ShouldReturnNotFound()
    {
        var result = await _mediator.Send(
            new GetUserByIdQuery(Guid.NewGuid().ToString()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal("Auth.User.NotFound", result.Errors[0].Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WhenUserIdIsMissingOrWhitespace_ShouldReturnValidationError(string? userId)
    {
        var result = await _mediator.Send(
            new GetUserByIdQuery(userId),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal("Auth.UserId.Invalid", result.Errors[0].Code);
    }

    [Fact]
    public async Task Handle_WhenUserExists_ShouldReturnItsProfile()
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await userManager.FindByEmailAsync("pm@localhost");
        Assert.NotNull(user);

        var result = await _mediator.Send(
            new GetUserByIdQuery(user.Id),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(user.Id, result.Value.UserId);
        Assert.Equal(user.Email, result.Value.Email);
    }
}
