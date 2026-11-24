using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.WorkOrders.common.AssignLabor;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.Workorders;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepaireTasks;
using MechanicShop.Tests.Common.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.AssignLabor;

[Collection(WebAppFactoryCollection.CollectionName)]
public class AssignLaborCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenWorkOrderDoesNotExist_ShouldReturnError()
    {
        var result = await _mediator.Send(
            new AssignLaborCommand(Guid.NewGuid(), Guid.NewGuid(), new byte[8]),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Handle_WhenLaborDoesNotExist_ShouldReturnError()
    {
        var order = await CreateWorkOrderAsync();

        var result = await _mediator.Send(
            new AssignLaborCommand(order.Id, Guid.NewGuid(), order.RowVersion),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal(order.LaborId, (await _context.WorkOrders.SingleAsync(
            item => item.Id == order.Id,
            TestContext.Current.CancellationToken)).LaborId);
    }

    [Fact]
    public async Task Handle_WhenLaborIsAvailable_ShouldUpdateAndPersistAssignment()
    {
        var order = await CreateWorkOrderAsync();
        var newLabor = EmployeeFactory.CreateLabor().Value;
        await _context.Employees.AddAsync(newLabor, TestContext.Current.CancellationToken);
        await _context.EmployeeBranchMemberships.AddAsync(
            MechanicShop.Domain.Branches.EmployeeBranchMembership.Create(
                newLabor.Id, MechanicShop.Tests.Common.Branches.TestBranchIds.Main, false).Value,
            TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        factory.WorkOrderNotifier.Clear();
        factory.WorkOrderNotifier.OnNotify = async _ =>
        {
            var savedOrder = await _context.WorkOrders.AsNoTracking().SingleAsync(
                item => item.Id == order.Id,
                TestContext.Current.CancellationToken);
            Assert.Equal(newLabor.Id, savedOrder.LaborId);
        };

        var result = await _mediator.Send(
            new AssignLaborCommand(order.Id, newLabor.Id, order.RowVersion),
            TestContext.Current.CancellationToken);

        var persisted = await _context.WorkOrders.AsNoTracking().SingleAsync(
            item => item.Id == order.Id,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(newLabor.Id, persisted.LaborId);
        Assert.Equal(1, factory.WorkOrderNotifier.CallCount);
    }

    [Fact]
    public async Task Handle_WhenWorkOrderHasNoLabor_ShouldAssignAndPersistLabor()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken,
            includeLabor: false);
        var labor = EmployeeFactory.CreateLabor().Value;
        await _context.Employees.AddAsync(labor, TestContext.Current.CancellationToken);
        await _context.EmployeeBranchMemberships.AddAsync(
            MechanicShop.Domain.Branches.EmployeeBranchMembership.Create(
                labor.Id, MechanicShop.Tests.Common.Branches.TestBranchIds.Main, false).Value,
            TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new AssignLaborCommand(order.Id, labor.Id, order.RowVersion),
            TestContext.Current.CancellationToken);

        var persisted = await _context.WorkOrders.AsNoTracking().SingleAsync(
            item => item.Id == order.Id,
            TestContext.Current.CancellationToken);
        Assert.False(result.IsError);
        Assert.Equal(labor.Id, persisted.LaborId);
    }

    [Fact]
    public async Task Handle_WhenLaborIsOccupiedDuringWorkOrder_ShouldReturnErrorAndKeepAssignment()
    {
        var occupiedOrder = await CreateWorkOrderAsync();
        var targetOrder = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            occupiedOrder.StartAtUtc,
            TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new AssignLaborCommand(targetOrder.Id, occupiedOrder.LaborId!.Value, targetOrder.RowVersion),
            TestContext.Current.CancellationToken);

        var persisted = await _context.WorkOrders.AsNoTracking().SingleAsync(
            item => item.Id == targetOrder.Id,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal(targetOrder.LaborId, persisted.LaborId);
    }

    private Task<WorkOrder> CreateWorkOrderAsync() => SubcutaneousTestData.CreateWorkOrderAsync(
        _context,
        cancellationToken: TestContext.Current.CancellationToken);
}
