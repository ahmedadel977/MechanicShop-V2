using FluentValidation;

namespace MechanicShop.Application.Features.Branches.Commands.AddEmployeeToBranch;

public sealed class AddEmployeeToBranchCommandValidator : AbstractValidator<AddEmployeeToBranchCommand>
{
    public AddEmployeeToBranchCommandValidator()
    {
        RuleFor(command => command.BranchId).NotEmpty();
        RuleFor(command => command.EmployeeId).NotEmpty();
    }
}
