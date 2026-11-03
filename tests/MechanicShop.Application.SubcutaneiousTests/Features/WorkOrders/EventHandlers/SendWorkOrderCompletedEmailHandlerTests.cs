using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.EventHandlers;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.Workorders.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.EventHandlers;

[Collection(WebAppFactoryCollection.CollectionName)]
public class SendWorkOrderCompletedEmailHandlerTests(WebAppFactory factory)
{
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenWorkOrderExists_ShouldSendEmailAndSmsToItsCustomer()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken);
        var logger = factory.Services.GetRequiredService<ILogger<SendWorkOrderCompletedEmailHandler>>();
        var handler = new SendWorkOrderCompletedEmailHandler(factory.Notifications, _context, logger);
        factory.Notifications.Clear();

        await handler.Handle(
            new WorkOrderCompleted { WorkOrderId = order.Id },
            TestContext.Current.CancellationToken);

        Assert.Equal([order.Vehicle!.Customer!.Email!], factory.Notifications.Emails);
        Assert.Equal([order.Vehicle.Customer.PhoneNumber!], factory.Notifications.SmsMessages);
    }

    [Fact]
    public async Task Handle_WhenWorkOrderDoesNotExist_ShouldNotSendNotifications()
    {
        var logger = factory.Services.GetRequiredService<ILogger<SendWorkOrderCompletedEmailHandler>>();
        var handler = new SendWorkOrderCompletedEmailHandler(factory.Notifications, _context, logger);
        factory.Notifications.Clear();

        await handler.Handle(
            new WorkOrderCompleted { WorkOrderId = Guid.NewGuid() },
            TestContext.Current.CancellationToken);

        Assert.Empty(factory.Notifications.Emails);
        Assert.Empty(factory.Notifications.SmsMessages);
    }

    [Fact]
    public async Task Handle_WhenEmailDeliveryFails_ShouldPropagateErrorAndNotSendSms()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken);
        var notificationService = new FailingEmailNotificationService();
        var logger = factory.Services.GetRequiredService<ILogger<SendWorkOrderCompletedEmailHandler>>();
        var handler = new SendWorkOrderCompletedEmailHandler(notificationService, _context, logger);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.Handle(
                new WorkOrderCompleted { WorkOrderId = order.Id },
                TestContext.Current.CancellationToken));

        Assert.Equal("Email delivery failed.", exception.Message);
        Assert.True(notificationService.EmailWasAttempted);
        Assert.Equal(0, notificationService.SmsCallCount);
    }

    private sealed class FailingEmailNotificationService : INotificationService
    {
        public bool EmailWasAttempted { get; private set; }
        public int SmsCallCount { get; private set; }

        public Task SendEmailAsync(string to, CancellationToken cancellationToken = default)
        {
            EmailWasAttempted = true;
            return Task.FromException(new InvalidOperationException("Email delivery failed."));
        }

        public Task SendSmsAsync(string phoneNumber, CancellationToken cancellationToken = default)
        {
            SmsCallCount++;
            return Task.CompletedTask;
        }
    }
}
