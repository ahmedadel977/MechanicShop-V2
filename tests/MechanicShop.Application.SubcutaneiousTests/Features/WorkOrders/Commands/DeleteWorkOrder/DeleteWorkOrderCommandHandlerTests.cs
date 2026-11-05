using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.common.DeleteWorkOrder;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.Workorders;
using MechanicShop.Domain.Workorders.Enums;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepaireTasks;
using MechanicShop.Tests.Common.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.DeleteWorkOrder;

[Collection(WebAppFactoryCollection.CollectionName)]
public class DeleteWorkOrderCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenWorkOrderDoesNotExist_ShouldReturnError()
    {
        var result = await _mediator.Send(
            new DeleteWorkOrderCommand(Guid.NewGuid(), new byte[8]),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Handle_WhenWorkOrderIsScheduled_ShouldDeleteIt()
    {
        var workOrder = await CreateWorkOrderAsync();
        factory.WorkOrderNotifier.Clear();
        factory.WorkOrderNotifier.OnNotify = async _ => Assert.False(await _context.WorkOrders.AnyAsync(
            order => order.Id == workOrder.Id,
            TestContext.Current.CancellationToken));

        var result = await _mediator.Send(
            new DeleteWorkOrderCommand(workOrder.Id, workOrder.RowVersion),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.False(await _context.WorkOrders.AnyAsync(
            order => order.Id == workOrder.Id,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, factory.WorkOrderNotifier.CallCount);
    }

    [Fact]
    public async Task Handle_WhenWorkOrderIsInProgress_ShouldReturnErrorAndKeepIt()
    {
        var workOrder = await CreateWorkOrderAsync();
        Assert.False(workOrder.UpdateState(WorkOrderState.InProgress).IsError);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new DeleteWorkOrderCommand(workOrder.Id, workOrder.RowVersion),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.True(await _context.WorkOrders.AnyAsync(
            order => order.Id == workOrder.Id,
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(WorkOrderState.Completed)]
    [InlineData(WorkOrderState.Cancelled)]
    public async Task Handle_WhenWorkOrderIsCompletedOrCancelled_ShouldReturnErrorAndKeepIt(WorkOrderState state)
    {
        var workOrder = await CreateWorkOrderAsync();
        if (state == WorkOrderState.Completed)
        {
            Assert.False(workOrder.UpdateState(WorkOrderState.InProgress).IsError);
        }
        Assert.False(workOrder.UpdateState(state).IsError);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new DeleteWorkOrderCommand(workOrder.Id, workOrder.RowVersion),
            TestContext.Current.CancellationToken);

        var persisted = await _context.WorkOrders.AsNoTracking().SingleAsync(
            order => order.Id == workOrder.Id,
            TestContext.Current.CancellationToken);
        Assert.True(result.IsError);
        Assert.Equal(state, persisted.State);
    }

    private async Task<WorkOrder> CreateWorkOrderAsync()
    {
        var customer = CustomerFactory.CreateCustomer().Value;
        Vehicle vehicle = customer.Vehicles.First();
        Employee labor = EmployeeFactory.CreateLabor().Value;
        RepairTask repairTask = RepairTaskFactory.CreateRepairTask().Value;
        var workOrder = WorkOrderFactory.CreateWorkOrder(
            vehicleId: vehicle.Id,
            laborId: labor.Id,
            repairTasks: [repairTask]).Value;
        vehicle.Customer = customer;
        workOrder.Vehicle = vehicle;
        workOrder.Labor = labor;

        await _context.Customers.AddAsync(customer, TestContext.Current.CancellationToken);
        await _context.Employees.AddAsync(labor, TestContext.Current.CancellationToken);
        await _context.RepairTasks.AddAsync(repairTask, TestContext.Current.CancellationToken);
        await _context.WorkOrders.AddAsync(workOrder, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return workOrder;
    }
}
