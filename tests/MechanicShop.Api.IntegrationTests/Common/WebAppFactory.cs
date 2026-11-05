using System.Collections;
using MechanicShop.Infrastructure.identity;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Infrastructure.BackgroundJobs;
using MechanicShop.Infrastructure.Date;
using MechanicShop.Tests.Common.Branches;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Testcontainers.MsSql;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace MechanicShop.Api.IntegrationTests.Common;

public class WebAppFactory : WebApplicationFactory<IAssemblyMarker>, IAsyncLifetime
{
    private readonly ConcurrentDictionary<string, Task<string>> _accessTokens = new();
    private readonly MsSqlContainer _dbContainer =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();
    private readonly IContainer _redisContainer = new ContainerBuilder("redis:7-alpine")
        .WithPortBinding(6379, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(6379))
        .Build();
    private string? _databaseConnectionString;
    private string? _redisConnectionString;

    public AppHttpClient CreateAppHttpClient()
    {
        return new AppHttpClient(CreateClient(), GetAccessTokenAsync);
    }

    private Task<string> GetAccessTokenAsync(AppUser user, CancellationToken cancellationToken) =>
        _accessTokens.GetOrAdd(user.Id, _ => GenerateAccessTokenAsync(user, cancellationToken));

    private async Task<string> GenerateAccessTokenAsync(AppUser user, CancellationToken cancellationToken)
    {
        using var client = new AppHttpClient(CreateClient());
        return await client.GenerateTokenFromEndpointAsync(user, cancellationToken);
    }

    public IServiceScope CreateScope() => Services.CreateScope();

    public Task PauseSqlContainerAsync() => _dbContainer.PauseAsync();

    public Task UnpauseSqlContainerAsync() => _dbContainer.UnpauseAsync();

    public Task PauseRedisContainerAsync() => _redisContainer.PauseAsync();

    public Task UnpauseRedisContainerAsync() => _redisContainer.UnpauseAsync();

    public async ValueTask InitializeAsync()
    {
        await _dbContainer.StartAsync();
        await _redisContainer.StartAsync();
        _databaseConnectionString = new SqlConnectionStringBuilder(_dbContainer.GetConnectionString())
        {
            InitialCatalog = "MechanicShopApiIntegrationTests"
        }.ConnectionString;
        _redisConnectionString = $"{_redisContainer.Hostname}:{_redisContainer.GetMappedPublicPort(6379)}";

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var initializer = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();
        await initializer.InitialiseAsync();
        await initializer.SeedAsync();

        await context.WorkOrders.IgnoreQueryFilters().ExecuteDeleteAsync(TestContext.Current.CancellationToken);
    }

    public new async ValueTask DisposeAsync()
    {
        await _redisContainer.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("JwtSettings:Secret", new string('t', 64));
        builder.UseSetting("JwtSettings:Issuer", "MechanicShop.Test");
        builder.UseSetting("JwtSettings:Audience", "MechanicShop.Test");
        builder.UseSetting("JwtSettings:TokenExpirationInMinutes", "60");
        builder.UseSetting(
            "ConnectionStrings:Redis",
            _redisConnectionString ?? $"{_redisContainer.Hostname}:{_redisContainer.GetMappedPublicPort(6379)}");

        builder.ConfigureTestServices(services =>
        {
            services.PostConfigure<RateLimiterOptions>(options =>
            {
                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, int>(
                    _ => RateLimitPartition.GetNoLimiter(0));
                var unactivatedPolicies = typeof(RateLimiterOptions)
                    .GetProperty("UnactivatedPolicyMap", BindingFlags.Instance | BindingFlags.NonPublic)?
                    .GetValue(options) as IDictionary;
                var activatedPolicies = typeof(RateLimiterOptions)
                    .GetProperty("PolicyMap", BindingFlags.Instance | BindingFlags.NonPublic)?
                    .GetValue(options) as IDictionary;
                foreach (var policyName in new[] { "LoginPolicy", "RefreshPolicy", "ApiUserPolicy", "RegisterPolicy" })
                {
                    unactivatedPolicies?.Remove(policyName);
                    activatedPolicies?.Remove(policyName);
                    options.AddPolicy(policyName, _ => RateLimitPartition.GetNoLimiter(0));
                }
            });

            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();

            services.RemoveAll<IHostedService>();
            services.RemoveAll<OverdueBookingCleanupService>();
            services.RemoveAll<ICurrentBranchContext>();
            services.AddScoped<ICurrentBranchContext, TestCurrentBranchContext>();

            services.RemoveAll<DbContextOptions<AppDbContext>>();

            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
                var connectionString = _databaseConnectionString ?? new SqlConnectionStringBuilder(_dbContainer.GetConnectionString())
                {
                    InitialCatalog = "MechanicShopApiIntegrationTests"
                }.ConnectionString;
                options.UseSqlServer(connectionString);
            });
        });
    }
}
