using FluentValidation.TestHelper;
using MechanicShop.Application.Features.ReaparTasks.Commands.CreateRepairTask;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Commands.CreateRepairTask;

public class CreateRepairTaskPartCommandValidatorTests
{
    private readonly CreateRepairTaskPartCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenMinimumCostAndQuantityAreUsed_ShouldSucceed()
    {
        var result = _validator.TestValidate(new CreateRepairTaskPartCommand("Part", 0, 1));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenQuantityIsZero_ShouldReportQuantityError()
    {
        var result = _validator.TestValidate(new CreateRepairTaskPartCommand("Part", 10, 0));
        result.ShouldHaveValidationErrorFor(part => part.Quantity);
    }

    [Fact]
    public void Validate_WhenNameExceedsMaximumLength_ShouldReportNameError()
    {
        var result = _validator.TestValidate(new CreateRepairTaskPartCommand(new string('x', 101), 10, 1));
        result.ShouldHaveValidationErrorFor(part => part.Name);
    }
}
