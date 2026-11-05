using FluentValidation;

namespace MechanicShop.Application.Features.WorkOrders.common.DeleteWorkOrder;

public sealed class DeleteWorkOrderCommandValidator : AbstractValidator<DeleteWorkOrderCommand>
{
    public DeleteWorkOrderCommandValidator()
    {
        RuleFor(x => x.WorkOrderId)
           .NotEmpty()
           .WithErrorCode("WorkOrderId_Required")
           .WithMessage("WorkOrderId is required.");

        RuleFor(x => x.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithErrorCode("WorkOrderRowVersion_Required")
            .WithMessage("A valid WorkOrder RowVersion is required.");
    }
}
