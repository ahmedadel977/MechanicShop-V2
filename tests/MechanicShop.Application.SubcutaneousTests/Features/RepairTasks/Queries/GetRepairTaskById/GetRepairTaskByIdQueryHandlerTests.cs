using MechanicShop.Application.Features.ReaparTasks.Queries.GerReapairTaskByid;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Tests.Common.RepaireTasks;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Queries.GetRepairTaskById;

[Collection(WebAppFactoryCollection.CollectionName)]
public class GetRepairTaskByIdQueryHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenRepairTaskDoesNotExist_ShouldReturnNotFound()
    {
        var result = await _mediator.Send(
            new GetRepairTaskByIdQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Handle_WhenRepairTaskExists_ShouldReturnItsPartsAndPersistedData()
    {
        var repairTask = RepairTaskFactory.CreateRepairTask().Value;
        await _context.RepairTasks.AddAsync(repairTask, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new GetRepairTaskByIdQuery(repairTask.Id),
            TestContext.Current.CancellationToken);

        var persisted = await _context.RepairTasks.AsNoTracking().SingleAsync(
            item => item.Id == repairTask.Id,
            TestContext.Current.CancellationToken);
        Assert.False(result.IsError);
        Assert.Equal(repairTask.Id, result.Value.RepairTaskId);
        Assert.Equal(persisted.Name, result.Value.Name);
        Assert.Equal(repairTask.Parts.Count(), result.Value.Parts.Count);
    }
}
