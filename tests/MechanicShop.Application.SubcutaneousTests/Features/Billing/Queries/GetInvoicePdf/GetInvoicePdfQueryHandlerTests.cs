using MechanicShop.Application.Features.Billing.Queries.GetInvoicePdf;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Tests.Common.Billing;
using MediatR;

namespace MechanicShop.Application.SubcutaneousTests.Features.Billing.Queries.GetInvoicePdf;

[Collection(WebAppFactoryCollection.CollectionName)]
public class GetInvoicePdfQueryHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenInvoiceDoesNotExist_ShouldReturnNotFound()
    {
        var result = await _mediator.Send(
            new GetInvoicePdfQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Handle_WhenInvoiceExists_ShouldReturnPdfContent()
    {
        var workOrder = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken);
        var invoice = InvoiceFactory.CreateInvoice(workOrderId: workOrder.Id).Value;
        invoice.WorkOrder = workOrder;
        await _context.Invoices.AddAsync(invoice, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new GetInvoicePdfQuery(invoice.Id),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal($"invoice-{invoice.Id}.pdf", result.Value.FileName);
        Assert.Equal("application/pdf", result.Value.ContentType);
        Assert.NotNull(result.Value.Content);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(result.Value.Content!, 0, 5));
    }
}
