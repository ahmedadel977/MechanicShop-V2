using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.RepairTasks.Queries.GetRepairTasks;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Tests.Common.RepaireTasks;
using MediatR;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Queries.GetRepairTasks;

[Collection(WebAppFactoryCollection.CollectionName)]
public class GetRepairTasksQueryHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenRepairTaskExists_ShouldReturnItWithParts()
    {
        var task = RepairTaskFactory.CreateRepairTask().Value;
        await _context.RepairTasks.AddAsync(task, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _mediator.Send(new GetRepairTasksQuery(), TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        var dto = Assert.Single(result.Value, item => item.RepairTaskId == task.Id);
        Assert.Equal(task.Name, dto.Name);
        Assert.Equal(task.Parts.Count(), dto.Parts.Count);
    }
}
