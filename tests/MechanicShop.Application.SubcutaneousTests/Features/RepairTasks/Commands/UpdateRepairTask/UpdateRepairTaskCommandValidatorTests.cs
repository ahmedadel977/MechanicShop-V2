using FluentValidation.TestHelper;
using MechanicShop.Application.Features.RepairTasks.Commands.UpdateRepairTask;
using MechanicShop.Domain.RepairTasks.Enums;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Commands.UpdateRepairTask;

public class UpdateRepairTaskCommandValidatorTests
{
    private readonly UpdateRepairTaskCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_ShouldSucceed()
    {
        var result = _validator.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenRepairTaskIdIsEmpty_ShouldReportRequiredError()
    {
        var result = _validator.TestValidate(ValidCommand() with { RepairTaskId = Guid.Empty });
        result.ShouldHaveValidationErrorFor(command => command.RepairTaskId);
    }

    [Fact]
    public void Validate_WhenLaborCostExceedsMaximum_ShouldReportCostError()
    {
        var result = _validator.TestValidate(ValidCommand() with { LaborCost = 10_001 });
        result.ShouldHaveValidationErrorFor(command => command.LaborCost);
    }

    private static UpdateRepairTaskCommand ValidCommand() => new(
        Guid.NewGuid(),
        "Inspection",
        100,
        RepairDurationInMinutes.Min60,
        [new UpdateRepairTaskPartCommand(null, "Oil filter", 10, 1)]);
}
