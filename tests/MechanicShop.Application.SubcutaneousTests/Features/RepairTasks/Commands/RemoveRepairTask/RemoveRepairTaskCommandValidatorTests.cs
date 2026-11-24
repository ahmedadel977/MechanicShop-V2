using FluentValidation.TestHelper;
using MechanicShop.Application.Features.RepairTasks.Commands.RemoveRepairTask;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Commands.RemoveRepairTask;

public class RemoveRepairTaskCommandValidatorTests
{
    private readonly RemoveRepairTaskCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenRepairTaskIdIsEmpty_ShouldReportRequiredError()
    {
        var result = _validator.TestValidate(new RemoveRepairTaskCommand(Guid.Empty));

        result.ShouldHaveValidationErrorFor(command => command.RepairTaskId)
            .WithErrorMessage("Repair task Id is required.");
    }

    [Fact]
    public void Validate_WhenRepairTaskIdIsProvided_ShouldSucceed()
    {
        var result = _validator.TestValidate(new RemoveRepairTaskCommand(Guid.NewGuid()));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
