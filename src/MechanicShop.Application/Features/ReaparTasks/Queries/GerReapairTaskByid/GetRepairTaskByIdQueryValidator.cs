using FluentValidation;

namespace MechanicShop.Application.Features.ReaparTasks.Queries.GerReapairTaskByid
{
    public class GetRepairTaskByIdQueryValidator : AbstractValidator<GetRepairTaskByIdQuery>
    {
        public GetRepairTaskByIdQueryValidator()
        {
            RuleFor(x => x.RepairTaskId).NotEmpty().WithMessage("Repair task ID is required.");
        }
    }
}


