using MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrders;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.Workorders;
using MechanicShop.Domain.Workorders.Enums;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepaireTasks;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Queries.GetWorkOrders;

[Collection(WebAppFactoryCollection.CollectionName)]
public class GetWorkOrdersQueryHandlerTests(WebAppFactory factory)
{
    private readonly MediatR.IMediator _mediator = factory.CreateMediator();
    private readonly MechanicShop.Application.Common.Interfaces.IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenFilteredByVehicle_ShouldReturnMatchingOrderAndPagination()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken);
        var query = new GetWorkOrdersQuery(1, 10, null, VehicleId: order.VehicleId);

        var result = await _mediator.Send(query, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Equal(1, result.Value.PageNumber);
        Assert.Equal(10, result.Value.PageSize);
        Assert.NotNull(result.Value.Items);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(order.Id, item.WorkOrderId);
        Assert.NotNull(item.Vehicle);
        Assert.NotNull(item.Customer);
    }

    [Fact]
    public async Task Handle_WhenSearchTermMatchesWorkOrderFields_ShouldReturnThatWorkOrder()
    {
        const string make = "UniqueSearchMake";
        const string model = "UniqueSearchModel";
        const string plate = "UniqueSearchPlate";
        const string firstName = "UniqueFirst";
        const string lastName = "UniqueLast";
        const string taskName = "UniqueSearchTask";
        var vehicle = VehicleFactory.CreateVehicle(make: make, model: model, licensePlate: plate).Value;
        var customer = CustomerFactory.CreateCustomer(
            email: $"{Guid.NewGuid():N}@example.test",
            vehicles: [vehicle]).Value;
        var labor = EmployeeFactory.CreateLabor(firstName: firstName, lastName: lastName).Value;
        var repairTask = RepairTaskFactory.CreateRepairTask(name: taskName).Value;
        var start = DateTimeOffset.UtcNow.Date.AddDays(3_000).AddHours(10);
        var order = WorkOrder.Create(
            Guid.NewGuid(), vehicle.Id, start, start.AddHours(1), labor.Id, Spot.A, [repairTask]).Value;
        order.Vehicle = vehicle;
        order.Labor = labor;
        await _context.Customers.AddAsync(customer, TestContext.Current.CancellationToken);
        await _context.Employees.AddAsync(labor, TestContext.Current.CancellationToken);
        await _context.RepairTasks.AddAsync(repairTask, TestContext.Current.CancellationToken);
        await _context.WorkOrders.AddAsync(order, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        foreach (var searchTerm in new[]
        {
            make,
            model,
            plate,
            firstName,
            lastName,
            $"{firstName} {lastName}",
            taskName,
            order.Id.ToString()
        })
        {
            var result = await _mediator.Send(
                new GetWorkOrdersQuery(1, 10, searchTerm),
                TestContext.Current.CancellationToken);

            Assert.False(result.IsError);
            Assert.NotNull(result.Value.Items);
            var item = Assert.Single(result.Value.Items!);
            Assert.Equal(order.Id, item.WorkOrderId);
        }
    }

    [Fact]
    public async Task Handle_WhenVehicleFilterHasNoMatches_ShouldReturnEmptyPage()
    {
        var result = await _mediator.Send(
            new GetWorkOrdersQuery(1, 10, null, VehicleId: Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.NotNull(result.Value.Items);
        Assert.Empty(result.Value.Items!);
        Assert.Equal(0, result.Value.TotalCount);
        Assert.Equal(0, result.Value.TotalPages);
    }
}
