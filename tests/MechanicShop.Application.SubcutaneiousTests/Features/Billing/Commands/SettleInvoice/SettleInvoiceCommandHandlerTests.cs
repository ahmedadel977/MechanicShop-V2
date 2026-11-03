using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Billing.Commands.SettleInvoice;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.Workorders.Billing;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Billing;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepaireTasks;
using MechanicShop.Tests.Common.WorkOrders;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.SubcutaneousTests.Features.Billing.Commands.SettleInvoice;

[Collection(WebAppFactoryCollection.CollectionName)]
public class SettleInvoiceCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenInvoiceDoesNotExist_ShouldReturnError()
    {
        var result = await _mediator.Send(
            new SettleInvoiceCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Handle_WhenInvoiceIsUnpaid_ShouldMarkItPaidAndPersistPaymentTime()
    {
        var invoice = await CreateInvoiceAsync();
        var beforePayment = DateTimeOffset.UtcNow;

        var result = await _mediator.Send(
            new SettleInvoiceCommand(invoice.Id),
            TestContext.Current.CancellationToken);

        var persisted = await _context.Invoices.AsNoTracking().SingleAsync(
            item => item.Id == invoice.Id,
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(InvoiceStatus.Paid, persisted.Status);
        Assert.NotNull(persisted.PaidAt);
        Assert.InRange(persisted.PaidAt.Value, beforePayment, DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Handle_WhenInvoiceIsAlreadyPaid_ShouldReturnError()
    {
        var invoice = await CreateInvoiceAsync();
        Assert.False(invoice.MarkAsPaid(TimeProvider.System).IsError);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new SettleInvoiceCommand(invoice.Id),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
    }

    private async Task<Invoice> CreateInvoiceAsync()
    {
        var customer = CustomerFactory.CreateCustomer().Value;
        Vehicle vehicle = customer.Vehicles.First();
        Employee labor = EmployeeFactory.CreateLabor().Value;
        RepairTask task = RepairTaskFactory.CreateRepairTask().Value;
        var workOrder = WorkOrderFactory.CreateWorkOrder(
            vehicleId: vehicle.Id,
            laborId: labor.Id,
            repairTasks: [task]).Value;
        vehicle.Customer = customer;
        workOrder.Vehicle = vehicle;
        workOrder.Labor = labor;
        await _context.Customers.AddAsync(customer, TestContext.Current.CancellationToken);
        await _context.Employees.AddAsync(labor, TestContext.Current.CancellationToken);
        await _context.RepairTasks.AddAsync(task, TestContext.Current.CancellationToken);
        await _context.WorkOrders.AddAsync(workOrder, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var invoice = InvoiceFactory.CreateInvoice(workOrderId: workOrder.Id).Value;
        invoice.WorkOrder = workOrder;
        await _context.Invoices.AddAsync(invoice, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return invoice;
    }
}
