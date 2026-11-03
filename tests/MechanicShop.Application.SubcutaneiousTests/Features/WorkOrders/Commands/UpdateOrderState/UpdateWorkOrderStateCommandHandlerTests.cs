using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.common.UpdateOrderState;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.Workorders.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.UpdateOrderState;

[Collection(WebAppFactoryCollection.CollectionName)]
public class UpdateWorkOrderStateCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenWorkOrderDoesNotExist_ShouldReturnError()
    {
        var result = await _mediator.Send(
            new UpdateWorkOrderStateCommand(Guid.NewGuid(), WorkOrderState.InProgress),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Handle_WhenScheduledOrderHasStarted_ShouldPersistInProgressState()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            DateTimeOffset.UtcNow.AddHours(-2),
            TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new UpdateWorkOrderStateCommand(order.Id, WorkOrderState.InProgress),
            TestContext.Current.CancellationToken);

        var persisted = await _context.WorkOrders.AsNoTracking().SingleAsync(
            item => item.Id == order.Id,
            TestContext.Current.CancellationToken);
        Assert.False(result.IsError);
        Assert.Equal(WorkOrderState.InProgress, persisted.State);
    }

    [Fact]
    public async Task Handle_WhenScheduledOrderHasNotStarted_ShouldRejectTransitionAndKeepState()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            DateTimeOffset.UtcNow.AddDays(2),
            TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new UpdateWorkOrderStateCommand(order.Id, WorkOrderState.InProgress),
            TestContext.Current.CancellationToken);

        var persisted = await _context.WorkOrders.AsNoTracking().SingleAsync(
            item => item.Id == order.Id,
            TestContext.Current.CancellationToken);
        Assert.True(result.IsError);
        Assert.Equal("WorkOrderErrors.StateTransitionNotAllowed", result.Errors[0].Code);
        Assert.Equal(WorkOrderState.Scheduled, persisted.State);
    }

    [Fact]
    public async Task Handle_WhenTransitionSkipsInProgress_ShouldKeepScheduledState()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            DateTimeOffset.UtcNow.AddHours(-2),
            TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new UpdateWorkOrderStateCommand(order.Id, WorkOrderState.Completed),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal(WorkOrderState.Scheduled, (await _context.WorkOrders.AsNoTracking().SingleAsync(
            item => item.Id == order.Id,
            TestContext.Current.CancellationToken)).State);
    }

    [Fact]
    public async Task Handle_WhenOrderBecomesCompleted_ShouldNotifyCustomerByEmailAndSms()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            DateTimeOffset.UtcNow.AddHours(-2),
            TestContext.Current.CancellationToken);
        var customer = order.Vehicle!.Customer!;
        factory.Notifications.Clear();

        var started = await _mediator.Send(
            new UpdateWorkOrderStateCommand(order.Id, WorkOrderState.InProgress),
            TestContext.Current.CancellationToken);
        Assert.False(started.IsError);
        factory.WorkOrderNotifier.Clear();

        var completed = await _mediator.Send(
            new UpdateWorkOrderStateCommand(order.Id, WorkOrderState.Completed),
            TestContext.Current.CancellationToken);

        Assert.False(completed.IsError);
        Assert.Contains(customer.Email!, factory.Notifications.Emails);
        Assert.Contains(customer.PhoneNumber!, factory.Notifications.SmsMessages);
        Assert.Equal(1, factory.WorkOrderNotifier.CallCount);
    }

    [Fact]
    public async Task Handle_WhenCompletedOrderIsCompletedAgain_ShouldRejectItWithoutSendingDuplicateNotifications()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            DateTimeOffset.UtcNow.AddHours(-2),
            TestContext.Current.CancellationToken);
        factory.Notifications.Clear();
        var started = await _mediator.Send(
            new UpdateWorkOrderStateCommand(order.Id, WorkOrderState.InProgress),
            TestContext.Current.CancellationToken);
        var completed = await _mediator.Send(
            new UpdateWorkOrderStateCommand(order.Id, WorkOrderState.Completed),
            TestContext.Current.CancellationToken);
        Assert.False(started.IsError);
        Assert.False(completed.IsError);

        var repeated = await _mediator.Send(
            new UpdateWorkOrderStateCommand(order.Id, WorkOrderState.Completed),
            TestContext.Current.CancellationToken);

        Assert.True(repeated.IsError);
        Assert.Single(factory.Notifications.Emails);
        Assert.Single(factory.Notifications.SmsMessages);
        Assert.Equal(WorkOrderState.Completed, (await _context.WorkOrders.AsNoTracking().SingleAsync(
            item => item.Id == order.Id,
            TestContext.Current.CancellationToken)).State);
    }
}
