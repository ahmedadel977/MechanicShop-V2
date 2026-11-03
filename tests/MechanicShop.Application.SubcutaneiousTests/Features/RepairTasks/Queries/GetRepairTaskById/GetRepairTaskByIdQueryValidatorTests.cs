using FluentValidation.TestHelper;
using MechanicShop.Application.Features.ReaparTasks.Queries.GerReapairTaskByid;

namespace MechanicShop.Application.SubcutaneousTests.Features.RepairTasks.Queries.GetRepairTaskById;

public class GetRepairTaskByIdQueryValidatorTests
{
    private readonly GetRepairTaskByIdQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenRepairTaskIdIsEmpty_ShouldReportRequiredError()
    {
        var result = _validator.TestValidate(new GetRepairTaskByIdQuery(Guid.Empty));
        result.ShouldHaveValidationErrorFor(query => query.RepairTaskId)
            .WithErrorMessage("Repair task ID is required.");
    }

    [Fact]
    public void Validate_WhenRepairTaskIdIsValid_ShouldSucceed()
    {
        var result = _validator.TestValidate(new GetRepairTaskByIdQuery(Guid.NewGuid()));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
