using FluentValidation;

namespace MechanicShop.Application.Features.ReaparTasks.Commands.CreateRepairTask
{
    public class CreateRepairTaskPartCommandValidator : AbstractValidator<CreateRepairTaskPartCommand>
    {
        public CreateRepairTaskPartCommandValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("Name is required.")
                .MaximumLength(100).WithMessage("Name cannot exceed 100 characters.");
            RuleFor(x => x.Cost)
                .GreaterThanOrEqualTo(0).WithMessage("Cost must be greater than or equal to 0.");
            RuleFor(x => x.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than 0.");
        }
    }
}
