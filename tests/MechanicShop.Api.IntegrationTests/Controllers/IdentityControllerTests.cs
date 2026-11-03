using System.Net;
using System.Net.Http.Json;

using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Api.IntegrationTests.Common;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using MechanicShop.Tests.Common.Security;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public class IdentityControllerTests(WebAppFactory webAppFactory)
{
    private readonly AppHttpClient _client = webAppFactory.CreateAppHttpClient();

    [Fact]
    public async Task Authorize_WhenPolicyNameIsNull_ShouldDenyAccess()
    {
        using var scope = webAppFactory.CreateScope();
        var identityService = scope.ServiceProvider.GetRequiredService<IIdentityService>();

        var authorized = await identityService.AuthorizeAsync(TestUsers.Manager.Id, null);

        Assert.False(authorized);
    }

[Fact]
    public async Task GetCurrentUserClaims_WhenAuthenticated_ShouldReturnManagerIdentity()
    {
        await AuthenticateAsManagerAsync();

        var response = await _client.GetAsync(
            "/identity/current-user/claims",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(TestUsers.Manager.Id, body);
    }

[Fact]
    public async Task GetCurrentUserClaims_WhenUnauthenticated_ShouldReturnUnauthorized()
    {
        var response = await _client.GetAsync(
            "/identity/current-user/claims",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

[Fact]
    public async Task RefreshToken_WhenTokensAreInvalid_ShouldReturnConflict()
    {
        var response = await _client.PostAsJsonAsync(
            "/identity/token/refresh-token",
            new { RefreshToken = "invalid-refresh-token", ExpiredAccessToken = "invalid-access-token" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

[Fact]
    public async Task GenerateToken_WhenCredentialsAreInvalid_ShouldReturnConflict()
    {
        var response = await _client.PostAsJsonAsync(
            "/identity/token/generate",
            new { Email = TestUsers.Manager.Email, Password = "incorrect-password" },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

[Fact]
    public async Task GenerateToken_WhenCredentialsAreEmpty_ShouldReturnBadRequest()
    {
        var response = await _client.PostAsJsonAsync(
            "/identity/token/generate",
            new { Email = string.Empty, Password = string.Empty },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

[Fact]
    public async Task GetCurrentUserClaims_WhenLaborIsAuthenticated_ShouldReturnLaborIdentity()
    {
        var token = await _client.GenerateTokenAsync(TestUsers.Labor01, TestContext.Current.CancellationToken);
        _client.SetAuthorizationHeader(token);

        var response = await _client.GetAsync(
            "/identity/current-user/claims",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains(TestUsers.Labor01.Id, body);
        Assert.Contains("Labor", body);
    }


    private async Task AuthenticateAsManagerAsync()
    {
        var token = await _client.GenerateTokenAsync(
            TestUsers.Manager,
            TestContext.Current.CancellationToken);

        _client.SetAuthorizationHeader(token);
    }
}
