using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.RepairTasks.Commands.RemoveRepairTask;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Tests.Common.RepaireTasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Commands.RemoveRepairTask;

[Collection(WebAppFactoryCollection.CollectionName)]
public class RemoveRepairTaskCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenRepairTaskDoesNotExist_ShouldReturnError()
    {
        var result = await _mediator.Send(
            new RemoveRepairTaskCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Handle_WhenRepairTaskIsNotInUse_ShouldDeleteIt()
    {
        var task = RepairTaskFactory.CreateRepairTask().Value;
        await _context.RepairTasks.AddAsync(task, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new RemoveRepairTaskCommand(task.Id),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.False(await _context.RepairTasks.AnyAsync(
            item => item.Id == task.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WhenRepairTaskIsUsedByWorkOrder_ShouldKeepIt()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken);
        var taskId = order.RepairTasks.Single().Id;

        var result = await _mediator.Send(
            new RemoveRepairTaskCommand(taskId),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.True(await _context.RepairTasks.AnyAsync(
            task => task.Id == taskId,
            TestContext.Current.CancellationToken));
    }
}
