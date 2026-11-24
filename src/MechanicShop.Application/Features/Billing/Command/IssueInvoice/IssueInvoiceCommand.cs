using MechanicShop.Application.Features.Billing.Dtos;
using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Workorders;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace MechanicShop.Domain.Billing.Command.IssueInvoice
{
    public sealed record IssueInvoiceCommand (Guid WorkOrderId) : IRequest<Result<InvoiceDto>>
    {
    }
}
