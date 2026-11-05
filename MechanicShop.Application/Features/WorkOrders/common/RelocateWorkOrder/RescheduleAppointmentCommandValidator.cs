using FluentValidation;

namespace MechanicShop.Application.Features.WorkOrders.common.RelocateWorkOrder;

public sealed class RescheduleAppointmentCommandValidator : AbstractValidator<RelocateWorkOrderCommand>
{
    public RescheduleAppointmentCommandValidator()
    {
        RuleFor(x => x.WorkOrderId)
            .NotEmpty();

        RuleFor(x => x.NewStartAt)
            .GreaterThan(DateTimeOffset.UtcNow)
            .WithMessage("New start time must be in the future.");

        RuleFor(x => x.NewSpot)
            .IsInEnum();

        RuleFor(x => x.RowVersion)
            .NotNull()
            .Must(rowVersion => rowVersion is { Length: 8 })
            .WithErrorCode("WorkOrderRowVersion_Required")
            .WithMessage("A valid WorkOrder RowVersion is required.");
    }
}
