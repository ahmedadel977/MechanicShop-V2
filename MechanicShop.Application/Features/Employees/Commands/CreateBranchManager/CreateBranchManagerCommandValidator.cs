using FluentValidation;

namespace MechanicShop.Application.Features.Employees.Commands.CreateBranchManager;

public sealed class CreateBranchManagerCommandValidator : AbstractValidator<CreateBranchManagerCommand>
{
    public CreateBranchManagerCommandValidator()
    {
        RuleFor(command => command.FirstName).NotEmpty().MaximumLength(50);
        RuleFor(command => command.LastName).NotEmpty().MaximumLength(50);
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(command => command.Password).NotEmpty().MinimumLength(6);
        RuleFor(command => command.BranchId).NotEmpty();
    }
}
