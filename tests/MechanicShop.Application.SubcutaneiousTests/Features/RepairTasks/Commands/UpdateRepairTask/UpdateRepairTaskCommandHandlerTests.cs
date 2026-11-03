using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.RepairTasks.Commands.UpdateRepairTask;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.RepairTasks.Enums;
using MechanicShop.Tests.Common.RepaireTasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Commands.UpdateRepairTask;

[Collection(WebAppFactoryCollection.CollectionName)]
public class UpdateRepairTaskCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenRepairTaskDoesNotExist_ShouldReturnError()
    {
        var result = await _mediator.Send(
            new UpdateRepairTaskCommand(Guid.NewGuid(), "Updated", 100, RepairDurationInMinutes.Min60,
                [new UpdateRepairTaskPartCommand(null, "Part", 10, 1)]),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Handle_WhenRepairTaskExists_ShouldUpdateTaskAndParts()
    {
        var task = RepairTaskFactory.CreateRepairTask().Value;
        var originalPart = task.Parts.First();
        await _context.RepairTasks.AddAsync(task, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new UpdateRepairTaskCommand(
            task.Id,
            "Updated inspection",
            250,
            RepairDurationInMinutes.Min60,
            [new UpdateRepairTaskPartCommand(originalPart.Id, "Updated part", 35, 2)]);
        var result = await _mediator.Send(command, TestContext.Current.CancellationToken);

        var persisted = await _context.RepairTasks.AsNoTracking()
            .Include(item => item.Parts)
            .SingleAsync(item => item.Id == task.Id, TestContext.Current.CancellationToken);
        Assert.False(result.IsError);
        Assert.Equal(command.Name, persisted.Name);
        Assert.Equal(command.LaborCost, persisted.LaborCost);
        var part = Assert.Single(persisted.Parts);
        Assert.Equal("Updated part", part.Name);
        Assert.Equal(35, part.Cost);
        Assert.Equal(2, part.Quantity);
    }
}
