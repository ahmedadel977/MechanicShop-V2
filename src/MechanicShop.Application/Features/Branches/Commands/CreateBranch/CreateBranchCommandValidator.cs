using FluentValidation;

namespace MechanicShop.Application.Features.Branches.Commands.CreateBranch;

public sealed class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty().MaximumLength(120);
        RuleFor(command => command.Code).NotEmpty().MaximumLength(20);
    }
}
