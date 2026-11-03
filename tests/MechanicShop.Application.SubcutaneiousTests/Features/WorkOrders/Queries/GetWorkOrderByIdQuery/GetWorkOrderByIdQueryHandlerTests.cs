using MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrderByIdQuery;
using MechanicShop.Application.SubcutaneousTests.Common;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Queries.GetWorkOrderByIdQuery;

[Collection(WebAppFactoryCollection.CollectionName)]
public class GetWorkOrderByIdQueryHandlerTests(WebAppFactory factory)
{
    private readonly MediatR.IMediator _mediator = factory.CreateMediator();
    private readonly MechanicShop.Application.Common.Interfaces.IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenWorkOrderDoesNotExist_ShouldReturnError()
    {
        var result = await _mediator.Send(
            new MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrderByIdQuery.GetWorkOrderByIdQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken);
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Handle_WhenWorkOrderExists_ShouldReturnItsRelatedData()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrderByIdQuery.GetWorkOrderByIdQuery(order.Id),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(order.Id, result.Value.WorkOrderId);
        Assert.Equal(order.VehicleId, result.Value.Vehicle!.VehicleId);
        Assert.Single(result.Value.RepairTasks);
        Assert.NotNull(result.Value.Labor);
    }

    [Fact]
    public async Task Handle_WhenWorkOrderHasNoLabor_ShouldReturnVehicleAndNullLabor()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken,
            includeLabor: false);

        var result = await _mediator.Send(
            new MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrderByIdQuery.GetWorkOrderByIdQuery(order.Id),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(order.Id, result.Value.WorkOrderId);
        Assert.NotNull(result.Value.Vehicle);
        Assert.Null(result.Value.Labor);
    }
}
