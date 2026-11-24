using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.ReaparTasks.Commands.CreateRepairTask;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.RepairTasks.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Commands.CreateRepairTask;

[Collection(WebAppFactoryCollection.CollectionName)]
public class CreateRepairTaskCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenCommandIsValid_ShouldPersistRepairTaskAndParts()
    {
        var command = new CreateRepairTaskCommand(
            $"Task {Guid.NewGuid():N}",
            125,
            RepairDurationInMinutes.Min60,
            [new CreateRepairTaskPartCommand("Oil filter", 10, 2)]);

        var result = await _mediator.Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        var persisted = await _context.RepairTasks
            .Include(task => task.Parts)
            .SingleAsync(task => task.Id == result.Value.RepairTaskId, TestContext.Current.CancellationToken);
        Assert.Equal(command.Name, persisted.Name);
        Assert.Equal(125, persisted.LaborCost);
        Assert.Equal("Oil filter", Assert.Single(persisted.Parts).Name);
    }

    [Fact]
    public async Task Handle_WhenNameAlreadyExists_ShouldReturnError()
    {
        var name = $"Duplicate {Guid.NewGuid():N}";
        var first = await _mediator.Send(Command(name), TestContext.Current.CancellationToken);
        Assert.False(first.IsError);

        var second = await _mediator.Send(Command(name), TestContext.Current.CancellationToken);

        Assert.True(second.IsError);
    }

    private static CreateRepairTaskCommand Command(string name) => new(
        name,
        100,
        RepairDurationInMinutes.Min60,
        [new CreateRepairTaskPartCommand("Generic part", 5, 1)]);
}
