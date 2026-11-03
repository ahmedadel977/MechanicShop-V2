using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.common.RelocateWorkOrder;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.Workorders.Enums;
using MechanicShop.Domain.Workorders;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.RepaireTasks;
using MechanicShop.Tests.Common.Employees;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.RelocateWorkOrder;

[Collection(WebAppFactoryCollection.CollectionName)]
public class RelocateWorkOrderCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenWorkOrderDoesNotExist_ShouldReturnError()
    {
        var result = await _mediator.Send(
            new RelocateWorkOrderCommand(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(2), Spot.B),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Handle_WhenNewSlotIsAvailable_ShouldPersistNewStartAndSpot()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken);
        var duration = order.EndAtUtc - order.StartAtUtc;
        var newStart = new DateTimeOffset(order.StartAtUtc.Date, order.StartAtUtc.Offset).AddDays(1).AddHours(11);
        factory.WorkOrderNotifier.Clear();

        var result = await _mediator.Send(
            new RelocateWorkOrderCommand(order.Id, newStart, Spot.B),
            TestContext.Current.CancellationToken);

        var persisted = await _context.WorkOrders.AsNoTracking().SingleAsync(
            item => item.Id == order.Id,
            TestContext.Current.CancellationToken);
        Assert.False(result.IsError);
        Assert.Equal(newStart, persisted.StartAtUtc);
        Assert.Equal(Spot.B, persisted.Spot);
        Assert.Equal(duration, persisted.EndAtUtc - persisted.StartAtUtc);
        Assert.Equal(1, factory.WorkOrderNotifier.CallCount);
    }

    [Fact]
    public async Task Handle_WhenWorkOrderHasNoLabor_ShouldPersistRelocation()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken,
            includeLabor: false);
        var newStart = order.StartAtUtc.AddDays(1);

        var result = await _mediator.Send(
            new RelocateWorkOrderCommand(order.Id, newStart, Spot.B),
            TestContext.Current.CancellationToken);

        var persisted = await _context.WorkOrders.AsNoTracking().SingleAsync(
            item => item.Id == order.Id,
            TestContext.Current.CancellationToken);
        Assert.False(result.IsError);
        Assert.Equal(newStart, persisted.StartAtUtc);
        Assert.Null(persisted.LaborId);
    }

    [Fact]
    public async Task Handle_WhenNewTimeConflictsWithAssignedLabor_ShouldKeepCurrentSchedule()
    {
        var occupied = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken);
        var customer = CustomerFactory.CreateCustomer().Value;
        var vehicle = customer.Vehicles.First();
        var repairTask = RepairTaskFactory.CreateRepairTask().Value;
        var start = occupied.StartAtUtc.AddHours(2);
        var target = WorkOrder.Create(
            Guid.NewGuid(), vehicle.Id, start, start.AddHours(1), occupied.LaborId, Spot.B, [repairTask]).Value;
        target.Vehicle = vehicle;
        target.Labor = occupied.Labor;
        await _context.Customers.AddAsync(customer, TestContext.Current.CancellationToken);
        await _context.RepairTasks.AddAsync(repairTask, TestContext.Current.CancellationToken);
        await _context.WorkOrders.AddAsync(target, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new RelocateWorkOrderCommand(target.Id, occupied.StartAtUtc, Spot.C),
            TestContext.Current.CancellationToken);

        var persisted = await _context.WorkOrders.AsNoTracking().SingleAsync(
            item => item.Id == target.Id,
            TestContext.Current.CancellationToken);
        Assert.True(result.IsError);
        Assert.Equal("Employee.LaborOccupied", result.TopError.Code);
        Assert.Equal(start, persisted.StartAtUtc);
        Assert.Equal(Spot.B, persisted.Spot);
    }

    [Fact]
    public async Task Handle_WhenNewTimeConflictsWithVehicle_ShouldKeepCurrentSchedule()
    {
        var occupied = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken);
        var labor = EmployeeFactory.CreateLabor().Value;
        var repairTask = RepairTaskFactory.CreateRepairTask().Value;
        var start = occupied.StartAtUtc.AddHours(2);
        var target = WorkOrder.Create(
            Guid.NewGuid(), occupied.VehicleId, start, start.AddHours(1), labor.Id, Spot.B, [repairTask]).Value;
        target.Vehicle = occupied.Vehicle;
        target.Labor = labor;
        await _context.Employees.AddAsync(labor, TestContext.Current.CancellationToken);
        await _context.RepairTasks.AddAsync(repairTask, TestContext.Current.CancellationToken);
        await _context.WorkOrders.AddAsync(target, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new RelocateWorkOrderCommand(target.Id, occupied.StartAtUtc, Spot.C),
            TestContext.Current.CancellationToken);

        var persisted = await _context.WorkOrders.AsNoTracking().SingleAsync(
            item => item.Id == target.Id,
            TestContext.Current.CancellationToken);
        Assert.True(result.IsError);
        Assert.Equal("Vehicle_Overlapping_WorkOrder", result.TopError.Code);
        Assert.Equal(start, persisted.StartAtUtc);
        Assert.Equal(Spot.B, persisted.Spot);
    }

    [Fact]
    public async Task Handle_WhenTargetSpotIsOccupied_ShouldReturnErrorAndKeepSchedule()
    {
        var occupiedOrder = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken);
        var targetOrder = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            occupiedOrder.StartAtUtc,
            TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new RelocateWorkOrderCommand(targetOrder.Id, occupiedOrder.StartAtUtc, occupiedOrder.Spot),
            TestContext.Current.CancellationToken);

        var persisted = await _context.WorkOrders.AsNoTracking().SingleAsync(
            item => item.Id == targetOrder.Id,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal(targetOrder.StartAtUtc, persisted.StartAtUtc);
        Assert.Equal(targetOrder.Spot, persisted.Spot);
    }
}
