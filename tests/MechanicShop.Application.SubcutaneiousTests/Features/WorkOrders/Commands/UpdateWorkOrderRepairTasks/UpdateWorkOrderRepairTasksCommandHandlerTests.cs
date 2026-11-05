using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.common.UpdateWorkOrderRepairTasks;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Tests.Common.RepaireTasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.UpdateWorkOrderRepairTasks;

[Collection(WebAppFactoryCollection.CollectionName)]
public class UpdateWorkOrderRepairTasksCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenWorkOrderDoesNotExist_ShouldReturnNotFound()
    {
        var result = await _mediator.Send(
            new UpdateWorkOrderRepairTasksCommand(Guid.NewGuid(), [Guid.NewGuid()], new byte[8]),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal("WorkOrder.NotFound", result.TopError.Code);
    }

    [Fact]
    public async Task Handle_WhenRepairTaskDoesNotExist_ShouldKeepExistingTasks()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken);
        var originalIds = order.RepairTasks.Select(task => task.Id).ToArray();

        var result = await _mediator.Send(
            new UpdateWorkOrderRepairTasksCommand(order.Id, [Guid.NewGuid()], order.RowVersion),
            TestContext.Current.CancellationToken);

        var persisted = await _context.WorkOrders.AsNoTracking()
            .Include(item => item.RepairTasks)
            .SingleAsync(item => item.Id == order.Id, TestContext.Current.CancellationToken);
        Assert.True(result.IsError);
        Assert.Equal(originalIds, persisted.RepairTasks.Select(task => task.Id));
    }

    [Fact]
    public async Task Handle_WhenRepairTaskIdIsDuplicated_ShouldKeepExistingTasks()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken);
        RepairTask replacement = RepairTaskFactory.CreateRepairTask().Value;
        await _context.RepairTasks.AddAsync(replacement, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var originalIds = order.RepairTasks.Select(task => task.Id).ToArray();

        var result = await _mediator.Send(
            new UpdateWorkOrderRepairTasksCommand(order.Id, [replacement.Id, replacement.Id], order.RowVersion),
            TestContext.Current.CancellationToken);

        var persisted = await _context.WorkOrders.AsNoTracking()
            .Include(item => item.RepairTasks)
            .SingleAsync(item => item.Id == order.Id, TestContext.Current.CancellationToken);
        Assert.True(result.IsError);
        Assert.Equal(originalIds, persisted.RepairTasks.Select(task => task.Id));
    }

    [Fact]
    public async Task Handle_WhenReplacementTaskIsValid_ShouldPersistItAndUpdateDuration()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken);
        RepairTask replacement = RepairTaskFactory.CreateRepairTask().Value;
        await _context.RepairTasks.AddAsync(replacement, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new UpdateWorkOrderRepairTasksCommand(order.Id, [replacement.Id], order.RowVersion),
            TestContext.Current.CancellationToken);

        var persisted = await _context.WorkOrders.AsNoTracking()
            .Include(item => item.RepairTasks)
            .SingleAsync(item => item.Id == order.Id, TestContext.Current.CancellationToken);
        Assert.False(result.IsError, string.Join("; ", result.Errors.Select(error => error.Description)));
        Assert.Equal([replacement.Id], persisted.RepairTasks.Select(task => task.Id));
        Assert.Equal((int)replacement.EstimatedDurationInMins, (persisted.EndAtUtc - persisted.StartAtUtc).TotalMinutes);
    }

    [Fact]
    public async Task Handle_WhenWorkOrderHasNoLabor_ShouldPersistReplacementTasksAndDuration()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken,
            includeLabor: false);
        var replacement = RepairTaskFactory.CreateRepairTask().Value;
        await _context.RepairTasks.AddAsync(replacement, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new UpdateWorkOrderRepairTasksCommand(order.Id, [replacement.Id], order.RowVersion),
            TestContext.Current.CancellationToken);

        var persisted = await _context.WorkOrders.AsNoTracking()
            .Include(item => item.RepairTasks)
            .SingleAsync(item => item.Id == order.Id, TestContext.Current.CancellationToken);
        Assert.False(result.IsError, string.Join("; ", result.Errors.Select(error => error.Description)));
        Assert.Null(persisted.LaborId);
        Assert.Equal([replacement.Id], persisted.RepairTasks.Select(task => task.Id));
    }
}
