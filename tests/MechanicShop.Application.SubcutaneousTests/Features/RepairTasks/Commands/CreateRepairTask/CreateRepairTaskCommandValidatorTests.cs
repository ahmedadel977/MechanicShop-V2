using FluentValidation.TestHelper;
using MechanicShop.Application.Features.ReaparTasks.Commands.CreateRepairTask;
using MechanicShop.Domain.RepairTasks.Enums;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Commands.CreateRepairTask;

public class CreateRepairTaskCommandValidatorTests
{
    private readonly CreateRepairTaskCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_ShouldSucceed()
    {
        var result = _validator.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenLaborCostIsNotPositive_ShouldReportLaborCostError()
    {
        var command = ValidCommand() with { LaborCost = 0 };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(item => item.LaborCost);
    }

    [Fact]
    public void Validate_WhenPartsAreEmpty_ShouldRequireAPart()
    {
        var command = ValidCommand() with { Parts = [] };
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(item => item.Parts)
            .WithErrorMessage("At least one part is required.");
    }

    private static CreateRepairTaskCommand ValidCommand() => new(
        "Inspection",
        100,
        RepairDurationInMinutes.Min60,
        [new CreateRepairTaskPartCommand("Oil filter", 0, 1)]);
}
