using MechanicShop.Api;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.SubcutaneousTests.Common;

using MechanicShop.Infrastructure.BackGroundJobs;
using MechanicShop.Infrastructure.Date;
using MechanicShop.Infrastructure.Settings;
using MechanicShop.Tests.Common.Branches;

using MediatR;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Collections.Concurrent;

using Testcontainers.MsSql;

using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Common;

public class WebAppFactory : WebApplicationFactory<IAssemblyMarker>, IAsyncLifetime
{
    private readonly ConcurrentBag<IServiceScope> _scopes = [];
    private readonly MsSqlContainer _dbContainer =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public IMediator CreateMediator()
    {
        var serviceScope = Services.CreateScope();
        _scopes.Add(serviceScope);

        return serviceScope.ServiceProvider.GetRequiredService<IMediator>();
    }

    public IAppDbContext CreateAppDbContext()
    {
        var serviceScope = Services.CreateScope();
        _scopes.Add(serviceScope);

        return serviceScope.ServiceProvider.GetRequiredService<IAppDbContext>();
    }

    public RecordingNotificationService Notifications =>
        Services.GetRequiredService<RecordingNotificationService>();

    public RecordingWorkOrderNotifier WorkOrderNotifier =>
        Services.GetRequiredService<RecordingWorkOrderNotifier>();

    public async ValueTask InitializeAsync()
    {
        await _dbContainer.StartAsync();

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var initializer = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();
        await initializer.InitialiseAsync();
        await initializer.SeedAsync();

        await context.WorkOrders.IgnoreQueryFilters().ExecuteDeleteAsync(TestContext.Current.CancellationToken);
    }

    public new async Task DisposeAsync()
    {
        while (_scopes.TryTake(out var scope))
        {
            scope.Dispose();
        }

        await base.DisposeAsync();
        await _dbContainer.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<OverdueBookingCleanupService>();
            services.RemoveAll<ICurrentBranchContext>();
            services.AddScoped<ICurrentBranchContext, TestCurrentBranchContext>();

            // تستخدم Application handlers الـHybridCache؛ نستخدم Cache داخل العملية بدلًا من Redis خارجي.
            services.RemoveAll<IDistributedCache>();
            services.AddDistributedMemoryCache();
            services.RemoveAll<INotificationService>();
            services.AddSingleton<RecordingNotificationService>();
            services.AddSingleton<INotificationService>(sp =>
                sp.GetRequiredService<RecordingNotificationService>());
            services.RemoveAll<IWorkOrderNotifier>();
            services.AddSingleton<RecordingWorkOrderNotifier>();
            services.AddSingleton<IWorkOrderNotifier>(sp =>
                sp.GetRequiredService<RecordingWorkOrderNotifier>());

            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>((sp, options) =>
            {
                options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
                var connectionString = new SqlConnectionStringBuilder(_dbContainer.GetConnectionString())
                {
                    InitialCatalog = "MechanicShopSubcutaneousTests"
                }.ConnectionString;
                options.UseSqlServer(connectionString);
            });

            services.RemoveAll<AppSettings>();

            // تطبيق قيم إعدادات الاختبار بعد الإعدادات الأساسية.
            services.PostConfigure<AppSettings>(opts =>
            {
                opts.OpeningTime = new TimeOnly(9, 0);
                opts.ClosingTime = new TimeOnly(18, 0);
            });
        });
    }
}
