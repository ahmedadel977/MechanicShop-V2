using MechanicShop.Api.IntegrationTests.Common;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Infrastructure.Date;
using MechanicShop.Infrastructure.Realtime;
using MechanicShop.Tests.Common.Branches;
using MechanicShop.Tests.Common.Security;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;
using System.Net;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public sealed class WorkOrderHubTests(WebAppFactory webAppFactory)
{
    [Fact]
    public async Task WorkOrderHub_AnonymousConnection_ShouldBeRejected()
    {
        await using var connection = CreateConnection();

        var exception = await Record.ExceptionAsync(
            () => connection.StartAsync(TestContext.Current.CancellationToken));

        Assert.NotNull(exception);
        Assert.Equal(HubConnectionState.Disconnected, connection.State);
    }

    [Fact]
    public async Task WorkOrderHub_NegotiationWithAccessToken_ShouldBeAuthorized()
    {
        using var client = webAppFactory.CreateClient();
        using var appClient = new AppHttpClient(client);
        var accessToken = await appClient.GenerateTokenAsync(TestUsers.Labor01, TestContext.Current.CancellationToken);
        using var response = await client.PostAsync(
            $"{WorkOrderHub.HubUrl}/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(accessToken)}",
            content: null,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WorkOrderHub_LaborReceivesNotificationsOnlyForAssignedOrders()
    {
        using var client = webAppFactory.CreateAppHttpClient();
        var accessToken = await client.GenerateTokenAsync(TestUsers.Labor01, TestContext.Current.CancellationToken);
        var notificationReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var connection = CreateConnection(accessToken);
        connection.On("WorkOrdersChanged", () => notificationReceived.TrySetResult(true));
        await connection.StartAsync(TestContext.Current.CancellationToken);

        using var scope = webAppFactory.Services.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<IWorkOrderNotifier>();
        await notifier.NotifyWorkOrdersChangedAsync(
            TestBranchIds.Main,
            Guid.Parse(TestUsers.Labor02.Id),
            TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(250), TestContext.Current.CancellationToken);
        Assert.False(notificationReceived.Task.IsCompleted);

        await notifier.NotifyWorkOrdersChangedAsync(
            TestBranchIds.Main,
            Guid.Parse(TestUsers.Labor01.Id),
            TestContext.Current.CancellationToken);
        Assert.True(await notificationReceived.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WorkOrderHub_WhenBranchIsInactiveAtConnection_ShouldNotJoinItsGroup()
    {
        using var scope = webAppFactory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifier = scope.ServiceProvider.GetRequiredService<IWorkOrderNotifier>();
        var branch = await context.Branches
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == TestBranchIds.Main, TestContext.Current.CancellationToken);
        var wasActive = branch.IsActive;

        try
        {
            branch.SetActive(false);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);

            using var client = webAppFactory.CreateAppHttpClient();
            var accessToken = await client.GenerateTokenAsync(TestUsers.Manager, TestContext.Current.CancellationToken);
            var notificationReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            await using var connection = CreateConnection(accessToken);
            connection.On("WorkOrdersChanged", () => notificationReceived.TrySetResult(true));
            await connection.StartAsync(TestContext.Current.CancellationToken);

            await notifier.NotifyWorkOrdersChangedAsync(
                TestBranchIds.Main,
                laborId: null,
                ct: TestContext.Current.CancellationToken);
            await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);

            Assert.False(notificationReceived.Task.IsCompleted);
        }
        finally
        {
            branch.SetActive(wasActive);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    private HubConnection CreateConnection(string? accessToken = null)
    {
        var hubUrl = $"http://localhost{WorkOrderHub.HubUrl}";
        if (accessToken is not null)
        {
            hubUrl += $"?access_token={Uri.EscapeDataString(accessToken)}";
        }

        return new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.WebSocketFactory = (context, cancellationToken) =>
                    new ValueTask<WebSocket>(webAppFactory.Server.CreateWebSocketClient()
                        .ConnectAsync(context.Uri, cancellationToken));
            })
            .Build();
    }
}
