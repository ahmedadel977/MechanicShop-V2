using FluentValidation.TestHelper;
using MechanicShop.Application.Features.RepairTasks.Commands.UpdateRepairTask;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Commands.UpdateRepairTask;

public class UpdateRepairTaskPartCommandValidatorTests
{
    private readonly UpdateRepairTaskPartCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenBoundaryValuesAreValid_ShouldSucceed()
    {
        var result = _validator.TestValidate(new UpdateRepairTaskPartCommand(null, "Part", 1, 1));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenCostExceedsMaximum_ShouldReportCostError()
    {
        var result = _validator.TestValidate(new UpdateRepairTaskPartCommand(null, "Part", 10_001, 1));
        result.ShouldHaveValidationErrorFor(part => part.Cost);
    }

    [Fact]
    public void Validate_WhenQuantityExceedsMaximum_ShouldReportQuantityError()
    {
        var result = _validator.TestValidate(new UpdateRepairTaskPartCommand(null, "Part", 1, 11));
        result.ShouldHaveValidationErrorFor(part => part.Quantity);
    }
}
