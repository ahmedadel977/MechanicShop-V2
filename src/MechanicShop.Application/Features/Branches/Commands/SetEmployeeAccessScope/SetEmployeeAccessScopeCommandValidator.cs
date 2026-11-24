using FluentValidation;

namespace MechanicShop.Application.Features.Branches.Commands.SetEmployeeAccessScope;

public sealed class SetEmployeeAccessScopeCommandValidator : AbstractValidator<SetEmployeeAccessScopeCommand>
{
    public SetEmployeeAccessScopeCommandValidator()
    {
        RuleFor(command => command.EmployeeId).NotEmpty();
        RuleFor(command => command.AccessScope).IsInEnum();
    }
}
