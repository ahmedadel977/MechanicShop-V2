using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MechanicShop.Api.IntegrationTests.Infrastructure;

public sealed class RateLimitingTests
{
    [Theory]
    [InlineData("LoginPolicy", "RateLimiting:LoginPermitLimit")]
    [InlineData("RefreshPolicy", "RateLimiting:RefreshPermitLimit")]
    [InlineData("ApiUserPolicy", "RateLimiting:AuthenticatedUserPermitLimit")]
    [InlineData("RegisterPolicy", "RateLimiting:AccountCreationPermitLimit")]
    public async Task EndpointPolicy_Returns429WithRetryAfterWhenLimitIsExceeded(
        string policyName,
        string permitLimitSetting)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [permitLimitSetting] = "1"
            })
            .Build();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAppRateLimiting(configuration);

        await using var app = builder.Build();
        app.UseRouting();
        app.UseRateLimiter();
        app.MapGet("/login", () => Results.Ok())
            .RequireRateLimiting(policyName);
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var client = app.GetTestClient();
        var firstResponse = await client.GetAsync("/login", TestContext.Current.CancellationToken);
        var rejectedResponse = await client.GetAsync("/login", TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status200OK, (int)firstResponse.StatusCode);
        Assert.Equal(StatusCodes.Status429TooManyRequests, (int)rejectedResponse.StatusCode);
        Assert.True(rejectedResponse.Headers.TryGetValues("Retry-After", out var values));
        Assert.True(int.Parse(values!.Single()) > 0);
    }

    [Fact]
    public async Task GlobalSlidingWindow_Returns429WhenGlobalLimitIsExceeded()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:GlobalPermitLimit"] = "1"
            })
            .Build();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAppRateLimiting(configuration);

        await using var app = builder.Build();
        app.UseRouting();
        app.UseRateLimiter();
        app.MapGet("/global", () => Results.Ok());
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var client = app.GetTestClient();
        using var first = await client.GetAsync("/global", TestContext.Current.CancellationToken);
        using var rejected = await client.GetAsync("/global", TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status200OK, (int)first.StatusCode);
        Assert.Equal(StatusCodes.Status429TooManyRequests, (int)rejected.StatusCode);
    }

    [Fact]
    public async Task GlobalConcurrencyLimit_RejectsRequestsWhileTheLimitIsOccupied()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:GlobalConcurrencyLimit"] = "1"
            })
            .Build();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAppRateLimiting(configuration);

        var requestStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRequest = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var app = builder.Build();
        app.UseRouting();
        app.UseRateLimiter();
        app.MapGet("/concurrent", async (CancellationToken cancellationToken) =>
        {
            requestStarted.TrySetResult(true);
            await releaseRequest.Task.WaitAsync(cancellationToken);
            return Results.Ok();
        });
        await app.StartAsync(TestContext.Current.CancellationToken);

        using var client = app.GetTestClient();
        try
        {
            var activeRequest = client.GetAsync("/concurrent", TestContext.Current.CancellationToken);
            await requestStarted.Task.WaitAsync(TestContext.Current.CancellationToken);
            using var rejected = await client.GetAsync("/concurrent", TestContext.Current.CancellationToken);

            Assert.Equal(StatusCodes.Status429TooManyRequests, (int)rejected.StatusCode);

            releaseRequest.TrySetResult(true);
            using var accepted = await activeRequest;
            Assert.Equal(StatusCodes.Status200OK, (int)accepted.StatusCode);
        }
        finally
        {
            releaseRequest.TrySetResult(true);
        }
    }

    [Fact]
    public void RateLimitSettings_RejectsNonPositiveLimitsAtRegistration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:GlobalConcurrencyLimit"] = "0"
            })
            .Build();

        Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddAppRateLimiting(configuration));
    }
}
