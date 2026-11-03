using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.Billing.Command.IssueInvoice;
using MechanicShop.Domain.Common.Constamts;
using MechanicShop.Domain.Workorders;
using MechanicShop.Domain.Workorders.Billing;
using MechanicShop.Domain.Workorders.Enums;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepaireTasks;
using MechanicShop.Tests.Common.WorkOrders;
// استخدم Namespace الـ Factories الموجود في اختباراتك السابقة.

using MediatR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MechanicShop.Application.SubcutaneousTests.Features.Billing.Commands.IssueInvoice;

[Collection(WebAppFactoryCollection.CollectionName)]
public class IssueInvoiceCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenWorkOrderDoesNotExist_ShouldReturnError()
    {
        // Arrange
        var command = new IssueInvoiceCommand(Guid.NewGuid());

        // Act
        var result = await _mediator.Send(
            command,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Handle_WhenWorkOrderIsNotCompleted_ShouldReturnError()
    {
        // Arrange
        var workOrder = await CreateWorkOrderAsync();

        var command = new IssueInvoiceCommand(workOrder.Id);

        // Act
        var result = await _mediator.Send(
            command,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.IsError);

        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(
                i => i.WorkOrderId == workOrder.Id,
                TestContext.Current.CancellationToken);

        Assert.Null(invoice);
    }

    [Fact]
    public async Task Handle_WhenWorkOrderIsCompleted_ShouldCreateInvoice()
    {
        // Arrange
        var workOrder = await CreateCompletedWorkOrderAsync();

        var command = new IssueInvoiceCommand(workOrder.Id);

        // Act
        var result = await _mediator.Send(
            command,
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsError);

        var invoice = await _context.Invoices
            .SingleOrDefaultAsync(
                i => i.WorkOrderId == workOrder.Id,
                TestContext.Current.CancellationToken);

        Assert.NotNull(invoice);
    }

    [Fact]
    public async Task Handle_WhenInvoiceIsIssued_ShouldCalculateSubtotalCorrectly()
    {
        // Arrange
        var workOrder = await CreateCompletedWorkOrderAsync();

        var expectedSubtotal = workOrder.RepairTasks.Sum(task =>
            task.LaborCost +
            task.Parts.Sum(part => part.Cost * part.Quantity));

        // Act
        var result = await _mediator.Send(
            new IssueInvoiceCommand(workOrder.Id),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsError);

        var invoice = await _context.Invoices
            .SingleAsync(
                i => i.WorkOrderId == workOrder.Id,
                TestContext.Current.CancellationToken);

        Assert.Equal(expectedSubtotal, invoice.Subtotal);
    }

    [Fact]
    public async Task Handle_WhenInvoiceIsIssued_ShouldCalculateTaxCorrectly()
    {
        // Arrange
        var workOrder = await CreateCompletedWorkOrderAsync();

        // Act
        var result = await _mediator.Send(
            new IssueInvoiceCommand(workOrder.Id),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsError);

        var invoice = await _context.Invoices
            .SingleAsync(
                i => i.WorkOrderId == workOrder.Id,
                TestContext.Current.CancellationToken);

        var expectedTax =
            invoice.Subtotal * MechanicShopConstants.TaxRate;

        Assert.Equal(expectedTax, invoice.TaxAmount);
    }

    [Fact]
    public async Task Handle_WhenInvoiceIsIssued_ShouldCalculateTotalCorrectly()
    {
        // Arrange
        var workOrder = await CreateCompletedWorkOrderAsync();

        // Act
        var result = await _mediator.Send(
            new IssueInvoiceCommand(workOrder.Id),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsError);

        var invoice = await _context.Invoices
            .SingleAsync(
                i => i.WorkOrderId == workOrder.Id,
                TestContext.Current.CancellationToken);

        var expectedTotal =
            invoice.Subtotal -
            invoice.DiscountAmount +
            invoice.TaxAmount;

        Assert.Equal(expectedTotal, invoice.Total);
    }

    [Fact]
    public async Task Handle_WhenInvoiceIsIssued_ShouldHaveUnpaidStatus()
    {
        // Arrange
        var workOrder = await CreateCompletedWorkOrderAsync();

        // Act
        var result = await _mediator.Send(
            new IssueInvoiceCommand(workOrder.Id),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsError);

        var invoice = await _context.Invoices
            .SingleAsync(
                i => i.WorkOrderId == workOrder.Id,
                TestContext.Current.CancellationToken);

        Assert.Equal(InvoiceStatus.Unpaid, invoice.Status);
        Assert.Null(invoice.PaidAt);
    }

    [Fact]
    public async Task Handle_WhenInvoiceIsIssued_ShouldPersistInvoice()
    {
        // Arrange
        var workOrder = await CreateCompletedWorkOrderAsync();

        // Act
        var result = await _mediator.Send(
            new IssueInvoiceCommand(workOrder.Id),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.IsError);

        var invoice = await _context.Invoices
            .SingleOrDefaultAsync(
                i => i.WorkOrderId == workOrder.Id,
                TestContext.Current.CancellationToken);

        Assert.NotNull(invoice);
        Assert.Equal(workOrder.Id, invoice.WorkOrderId);
        Assert.NotEqual(Guid.Empty, invoice.Id);
    }

    [Fact]
    public async Task Handle_WhenInvoiceAlreadyExists_ShouldReturnConflictWithoutCreatingDuplicate()
    {
        var workOrder = await CreateCompletedWorkOrderAsync();
        var first = await _mediator.Send(
            new IssueInvoiceCommand(workOrder.Id),
            TestContext.Current.CancellationToken);
        Assert.False(first.IsError);

        var second = await _mediator.Send(
            new IssueInvoiceCommand(workOrder.Id),
            TestContext.Current.CancellationToken);

        Assert.True(second.IsError);
        Assert.Equal("Invoice.AlreadyIssued", second.TopError.Code);
        Assert.Equal(1, await _context.Invoices.CountAsync(
            invoice => invoice.WorkOrderId == workOrder.Id,
            TestContext.Current.CancellationToken));
    }

    private async Task<WorkOrder> CreateWorkOrderAsync()
    {
        // Arrange
        var customer = CustomerFactory.CreateCustomer().Value;

        var vehicle = customer.Vehicles.First();

        var labor = EmployeeFactory.CreateLabor().Value;

        var repairTask = RepairTaskFactory.CreateRepairTask().Value;

        var workOrder = WorkOrderFactory.CreateWorkOrder(
            vehicleId: vehicle.Id,
            laborId: labor.Id,
            repairTasks: [repairTask]).Value;

        vehicle.Customer = customer;

        workOrder.Vehicle = vehicle;
        workOrder.Labor = labor;

        await _context.Customers.AddAsync(
            customer,
            TestContext.Current.CancellationToken);

        await _context.Employees.AddAsync(
            labor,
            TestContext.Current.CancellationToken);

        await _context.RepairTasks.AddAsync(
            repairTask,
            TestContext.Current.CancellationToken);

        await _context.WorkOrders.AddAsync(
            workOrder,
            TestContext.Current.CancellationToken);

        await _context.SaveChangesAsync(
            TestContext.Current.CancellationToken);

        return workOrder;
    }

    private async Task<WorkOrder> CreateCompletedWorkOrderAsync()
    {
        // Arrange
        var workOrder = await CreateWorkOrderAsync();

        // Act
        var inProgressResult =
            workOrder.UpdateState(WorkOrderState.InProgress);

        Assert.False(inProgressResult.IsError);

        var completedResult =
            workOrder.UpdateState(WorkOrderState.Completed);

        Assert.False(completedResult.IsError);

        await _context.SaveChangesAsync(
            TestContext.Current.CancellationToken);

        return workOrder;
    }
}
