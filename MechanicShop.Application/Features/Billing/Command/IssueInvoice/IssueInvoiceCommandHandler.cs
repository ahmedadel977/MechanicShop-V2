using MechanicShop.Application.Common.Errors;
using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Billing.Dtos;
using MechanicShop.Application.Features.Billing.Mappers;
using MechanicShop.Application.Features.WorkOrders.Mappers;
using MechanicShop.Domain.Common.Constamts;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Workorders.Billing;
using MechanicShop.Domain.Workorders.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace MechanicShop.Domain.Billing.Command.IssueInvoice
{
    public sealed class IssueInvoiceCommandHandler : IRequestHandler<IssueInvoiceCommand, Result<InvoiceDto>>
    {
        private readonly IAppDbContext _context;
        private readonly ILogger _logger;
        private readonly HybridCache _cache;
        private readonly TimeProvider _timeProvider;
        
        public IssueInvoiceCommandHandler(IAppDbContext context, ILogger <IssueInvoiceCommandHandler> logger,HybridCache cache,TimeProvider timeProvider)
        {
            _context = context;
            _logger = logger;
            _cache = cache;
            _timeProvider = timeProvider;
        }
        public async Task<Result<InvoiceDto>> Handle(IssueInvoiceCommand request, CancellationToken cancellationToken)
        {
            var workOrder = await _context.WorkOrders
                .Include(w => w.Vehicle!)
                    .ThenInclude(v => v.Customer)
              .Include(w => w.RepairTasks)
                .ThenInclude(rt => rt.Parts)
              .FirstOrDefaultAsync(w => w.Id == request.WorkOrderId, cancellationToken  );
            if(workOrder is null)
            {
                _logger.LogWarning("Work order with ID {WorkOrderId} not found.", request.WorkOrderId);
                return ApplicationErrors.WorkOrderNotFound;
            }
            if (workOrder.State != WorkOrderState.Completed)
            {
                _logger.LogWarning("Work order with ID {WorkOrderId} is not completed. Current state: {State}", request.WorkOrderId, workOrder.State);
                return ApplicationErrors.WorkOrderMustBeCompletedForInvoicing;
            }

            if (await _context.Invoices.AnyAsync(
                    invoice => invoice.WorkOrderId == workOrder.Id,
                    cancellationToken))
            {
                return ApplicationErrors.InvoiceAlreadyIssued;
            }

            Guid invoiceId = Guid.NewGuid();

            var lineItems = new List<InvoiceLineItem>();

            var lineNumber = 1;
            var taskIndex = 1;
            foreach (var task in workOrder.RepairTasks)
            {
                var partsSummary = task.Parts.Any()
        ? string.Join(
            Environment.NewLine,
            task.Parts.Select(p => $"    • {p.Name} x{p.Quantity} @ {p.Cost:C}"))
        : "    • No parts";

                var lineDescription =
                    $"{taskIndex}: {task.Name}{Environment.NewLine}" +
                    $"  Labor = {task.LaborCost:C}{Environment.NewLine}" +
                    $"  Parts:{Environment.NewLine}{partsSummary}";

                var totalPartsCost = task.Parts.Sum(p => p.Cost * p.Quantity);

                var totalTaskCost = task.LaborCost + totalPartsCost;

                var lineItemResult = InvoiceLineItem.Create(
                    invoiceId: invoiceId,
                    lineNumber: lineNumber++,
                    description: lineDescription,
                    quantity: 1,
                    unitPrice: totalTaskCost);

                if (lineItemResult.IsError)
                {
                    return lineItemResult.Errors;
                }

                lineItems.Add(lineItemResult.Value);

                taskIndex++;


            }
            var discountAmount = workOrder.Discount ?? 0m;
            var subtotal = lineItems.Sum(li => li.LineTotal);
            var taxAmount = subtotal * MechanicShopConstants.TaxRate;
            var createInvoiceResult = Invoice.Create(
            id: invoiceId,
            workOrderId: workOrder.Id,
            items: lineItems,
            discountAmount: discountAmount,
            taxAmount: taxAmount,
            datetime: _timeProvider);
            if (createInvoiceResult.IsError)
            {
                _logger.LogWarning(
                 "Invoice creation failed for WorkOrderId: {WorkOrderId}. Errors: {@Errors}",
                 request.WorkOrderId,
                 createInvoiceResult.Errors);
                return createInvoiceResult.Errors;
            }
            var invoice = createInvoiceResult.Value;
            await _context.Invoices.AddAsync(createInvoiceResult.Value, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            await _cache.RemoveByTagAsync("invoice", cancellationToken);
            _logger.LogInformation("Invoice {InvoiceId} issued for WorkOrderId: {WorkOrderId}", invoiceId, request.WorkOrderId);
            return invoice.ToDto();
        }
    }
}
