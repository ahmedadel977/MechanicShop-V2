using MechanicShop.Application.Features.Dashbord.Queris.GetWOkorderstats;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Tests.Common.Billing;
using MechanicShop.Domain.Workorders.Billing;
using MechanicShop.Domain.Workorders.Enums;

namespace MechanicShop.Application.SubcutaneousTests.Features.Dashboard.Queries.GetWorkOrderStats;

[Collection(WebAppFactoryCollection.CollectionName)]
public class GetWorkOrderStatsQueryHandlerTests(WebAppFactory factory)
{
    private readonly MediatR.IMediator _mediator = factory.CreateMediator();
    private readonly MechanicShop.Application.Common.Interfaces.IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenDateHasNoOrders_ShouldReturnZeroedStatistics()
    {
        var date = new DateOnly(2001, 1, 1);

        var result = await _mediator.Send(
            new GetWorkorderStatsQuery(date),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(date, result.Value.Date);
        Assert.Equal(0, result.Value.Total);
        Assert.Equal(0, result.Value.TotalRevenue);
        Assert.Equal(0, result.Value.TotalPartsCost);
        Assert.Equal(0, result.Value.TotalLaborCost);
        Assert.Equal(0, result.Value.CompletionRate);
        Assert.Equal(0, result.Value.CancellationRate);
    }

    [Fact]
    public async Task Handle_WhenDateHasOneWorkOrder_ShouldAggregateItsCostsAndState()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken);
        var date = DateOnly.FromDateTime(order.StartAtUtc.UtcDateTime);
        var expectedParts = order.RepairTasks.Sum(task => task.Parts.Sum(part => part.Cost));
        var expectedLabor = order.RepairTasks.Sum(task => task.LaborCost);
        var invoice = InvoiceFactory.CreateInvoice(workOrderId: order.Id).Value;
        invoice.WorkOrder = order;
        await _context.Invoices.AddAsync(invoice, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new GetWorkorderStatsQuery(date),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(1, result.Value.Total);
        Assert.Equal(1, result.Value.Scheduled);
        Assert.Equal(0, result.Value.Completed);
        Assert.Equal(1, result.Value.UniqueVehicles);
        Assert.Equal(1, result.Value.UniqueCustomers);
        Assert.Equal(expectedParts, result.Value.TotalPartsCost);
        Assert.Equal(expectedLabor, result.Value.TotalLaborCost);
        Assert.Equal(invoice.Total, result.Value.TotalRevenue);
    }

    [Fact]
    public async Task Handle_WhenDateHasMixedStatesAndInvoices_ShouldAggregateDailyTotals()
    {
        var start = DateTimeOffset.UtcNow.Date.AddDays(2_000).AddHours(9);
        var scheduled = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context, start, TestContext.Current.CancellationToken);
        var inProgress = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context, start.AddHours(2), TestContext.Current.CancellationToken);
        Assert.False(inProgress.UpdateState(WorkOrderState.InProgress).IsError);
        var completed = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context, start.AddHours(4), TestContext.Current.CancellationToken);
        Assert.False(completed.UpdateState(WorkOrderState.InProgress).IsError);
        Assert.False(completed.UpdateState(WorkOrderState.Completed).IsError);
        var cancelled = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context, start.AddHours(6), TestContext.Current.CancellationToken);
        Assert.False(cancelled.UpdateState(WorkOrderState.Cancelled).IsError);

        var firstInvoiceId = Guid.NewGuid();
        var firstInvoice = InvoiceFactory.CreateInvoice(
            id: firstInvoiceId,
            workOrderId: scheduled.Id,
            items: [InvoiceLineItem.Create(firstInvoiceId, 1, "First service", 2, 50).Value],
            discount: 10,
            taxAmount: 5).Value;
        firstInvoice.WorkOrder = scheduled;
        var secondInvoiceId = Guid.NewGuid();
        var secondInvoice = InvoiceFactory.CreateInvoice(
            id: secondInvoiceId,
            workOrderId: completed.Id,
            items: [InvoiceLineItem.Create(secondInvoiceId, 1, "Second service", 1, 200).Value],
            discount: 0,
            taxAmount: 20).Value;
        secondInvoice.WorkOrder = completed;
        await _context.Invoices.AddRangeAsync(firstInvoice, secondInvoice);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var orders = new[] { scheduled, inProgress, completed, cancelled };
        var partsCost = orders.Sum(order => order.RepairTasks.Sum(task => task.Parts.Sum(part => part.Cost)));
        var laborCost = orders.Sum(order => order.RepairTasks.Sum(task => task.LaborCost));
        const decimal revenue = 315;
        var netProfit = revenue - partsCost - laborCost;
        var date = DateOnly.FromDateTime(start.Date);

        var result = await _mediator.Send(
            new GetWorkorderStatsQuery(date),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(4, result.Value.Total);
        Assert.Equal(1, result.Value.Scheduled);
        Assert.Equal(1, result.Value.InProgress);
        Assert.Equal(1, result.Value.Completed);
        Assert.Equal(1, result.Value.Cancelled);
        Assert.Equal(revenue, result.Value.TotalRevenue);
        Assert.Equal(partsCost, result.Value.TotalPartsCost);
        Assert.Equal(laborCost, result.Value.TotalLaborCost);
        Assert.Equal(netProfit, result.Value.NetProfit);
        Assert.Equal(netProfit / revenue * 100, result.Value.ProfitMargin);
        Assert.Equal(25, result.Value.CompletionRate);
        Assert.Equal(revenue / 4, result.Value.AverageRevenuePerOrder);
        Assert.Equal(1, result.Value.OrdersPerVehicle);
        Assert.Equal(partsCost / revenue * 100, result.Value.PartsCostRatio);
        Assert.Equal(laborCost / revenue * 100, result.Value.LaborCostRatio);
        Assert.Equal(0.25m, result.Value.CancellationRate);
    }

    [Fact]
    public async Task Handle_WhenOrdersHaveNoInvoices_ShouldReturnZeroRevenueRatios()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken);
        var date = DateOnly.FromDateTime(order.StartAtUtc.UtcDateTime);
        var partsCost = order.RepairTasks.Sum(task => task.Parts.Sum(part => part.Cost));
        var laborCost = order.RepairTasks.Sum(task => task.LaborCost);

        var result = await _mediator.Send(
            new GetWorkorderStatsQuery(date),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(1, result.Value.Total);
        Assert.Equal(0, result.Value.TotalRevenue);
        Assert.Equal(-partsCost - laborCost, result.Value.NetProfit);
        Assert.Equal(0, result.Value.ProfitMargin);
        Assert.Equal(0, result.Value.AverageRevenuePerOrder);
        Assert.Equal(0, result.Value.PartsCostRatio);
        Assert.Equal(0, result.Value.LaborCostRatio);
    }
}
