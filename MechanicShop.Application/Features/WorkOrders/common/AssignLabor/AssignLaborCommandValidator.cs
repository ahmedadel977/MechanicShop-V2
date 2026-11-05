using FluentValidation;

namespace MechanicShop.Application.Features.WorkOrders.common.AssignLabor;

public sealed class AssignLaborCommandValidator : AbstractValidator<AssignLaborCommand>
{
    public AssignLaborCommandValidator()
    {
        RuleFor(x => x.WorkOrderId)
         .NotEmpty()
         .WithErrorCode("WorkOrderId_Required")
         .WithMessage("WorkOrderId is required.");

        RuleFor(x => x.LaborId)
           .NotEmpty()
           .WithErrorCode("LaborId_Required")
           .WithMessage("LaborId is required.");

        RuleFor(x => x.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithErrorCode("WorkOrderRowVersion_Required")
            .WithMessage("A valid WorkOrder RowVersion is required.");
    }
}
