using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Billing.Queries.GetInvoiceById;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Tests.Common.Billing;
using MediatR;

namespace MechanicShop.Application.SubcutaneousTests.Features.Billing.Queries.GetInvoiceById;

[Collection(WebAppFactoryCollection.CollectionName)]
public class GetInvoiceByIdQueryHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenInvoiceDoesNotExist_ShouldReturnNotFound()
    {
        var result = await _mediator.Send(
            new GetInvoiceByIdQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken);
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Handle_WhenInvoiceExists_ShouldReturnInvoiceWithWorkOrderCustomerAndItems()
    {
        var workOrder = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken);
        var invoice = InvoiceFactory.CreateInvoice(workOrderId: workOrder.Id).Value;
        invoice.WorkOrder = workOrder;
        await _context.Invoices.AddAsync(invoice, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new GetInvoiceByIdQuery(invoice.Id),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(invoice.Id, result.Value.InvoiceId);
        Assert.Equal(workOrder.Id, result.Value.WorkOrderId);
        Assert.NotNull(result.Value.Customer);
        Assert.NotNull(result.Value.Vehicle);
        Assert.Single(result.Value.Items);
    }
}
