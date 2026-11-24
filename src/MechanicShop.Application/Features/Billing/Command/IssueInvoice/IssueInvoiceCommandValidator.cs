using FluentValidation;

namespace MechanicShop.Domain.Billing.Command.IssueInvoice
{
    public class IssueInvoiceCommandValidator : AbstractValidator<IssueInvoiceCommand>
    {
        public IssueInvoiceCommandValidator()
        {
            RuleFor(x => x.WorkOrderId).NotEmpty().WithMessage("WorkOrderId is required.");
        }
    }
}
