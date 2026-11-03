using FluentValidation;

namespace MechanicShop.Application.Features.WorkOrders.common.CreateWorkORDER
{
    public class CreateWorkOrderCommandValidator : AbstractValidator<CreateWorkOrderCommand>
    {
        public CreateWorkOrderCommandValidator()
        {
            RuleFor(x => x.VehicleId).NotEmpty().WithMessage("VehicleId is required.");
            RuleFor(x => x.StartAt).GreaterThanOrEqualTo(DateTimeOffset.UtcNow).WithMessage("StartAt must be a future date.");
            RuleFor(x => x.RepairTaskIds).NotEmpty().WithMessage("RepairTaskIds are required.");
            RuleFor(x => x.Spot).IsInEnum().WithMessage("Spot must be a valid enum value.");
            RuleFor(request => request.LaborId)
            .Must(laborId => laborId is null || laborId != Guid.Empty)
            .WithMessage("If provided, LaborId must not be empty.");

        }
    }
}
