using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using System.Net.WebSockets;

using MechanicShop.Api.IntegrationTests.Common;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Infrastructure.Realtime;
using Xunit;

namespace MechanicShop.Api.IntegrationTests.Controllers;

[Collection(WebAppFactoryCollection.CollectionName)]
public sealed class WorkOrderHubTests(WebAppFactory webAppFactory)
{
    [Fact]
    public async Task WorkOrderHub_WhenAnonymousClientReconnects_ShouldReceiveNotificationsAfterEachConnection()
    {
        var firstNotificationReceived = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondNotificationReceived = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var notificationCount = 0;

        await using var connection = new HubConnectionBuilder()
            .WithUrl($"http://localhost{WorkOrderHub.HubUrl}", options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.WebSocketFactory = (context, cancellationToken) =>
                    new ValueTask<WebSocket>(webAppFactory.Server.CreateWebSocketClient()
                        .ConnectAsync(context.Uri, cancellationToken));
            })
            .Build();

        connection.On("WorkOrdersChanged", () =>
        {
            var currentCount = Interlocked.Increment(ref notificationCount);
            if (currentCount == 1)
            {
                firstNotificationReceived.TrySetResult(true);
            }
            else if (currentCount == 2)
            {
                secondNotificationReceived.TrySetResult(true);
            }
        });

        await connection.StartAsync(TestContext.Current.CancellationToken);

        using var scope = webAppFactory.Services.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<IWorkOrderNotifier>();
        await notifier.NotifyWorkOrdersChangedAsync(TestContext.Current.CancellationToken);
        Assert.True(await firstNotificationReceived.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken));

        await connection.StopAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HubConnectionState.Disconnected, connection.State);
        await connection.StartAsync(TestContext.Current.CancellationToken);
        await notifier.NotifyWorkOrdersChangedAsync(TestContext.Current.CancellationToken);

        Assert.True(await secondNotificationReceived.Task.WaitAsync(
            TimeSpan.FromSeconds(5),
            TestContext.Current.CancellationToken));
        Assert.Equal(2, notificationCount);

        await connection.StopAsync(TestContext.Current.CancellationToken);
        Assert.Equal(HubConnectionState.Disconnected, connection.State);
    }
}
