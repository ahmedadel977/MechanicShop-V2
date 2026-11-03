using FluentValidation.TestHelper;
using MechanicShop.Application.Features.WorkOrders.common.UpdateWorkOrderRepairTasks;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.UpdateWorkOrderRepairTasks;

public class UpdateWorkOrderRepairTasksCommandValidatorTests
{
    private readonly UpdateWorkOrderRepairTasksCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenWorkOrderIdIsEmpty_ShouldReportError()
    {
        var result = _validator.TestValidate(new UpdateWorkOrderRepairTasksCommand(Guid.Empty, [Guid.NewGuid()]));

        result.ShouldHaveValidationErrorFor(command => command.WorkOrderId)
            .WithErrorCode("WorkOrderId_Required");
    }

    [Fact]
    public void Validate_WhenRepairTaskIdsAreEmpty_ShouldReportError()
    {
        var result = _validator.TestValidate(new UpdateWorkOrderRepairTasksCommand(Guid.NewGuid(), []));

        result.ShouldHaveValidationErrorFor(command => command.RepairTaskIds)
            .WithErrorCode("RepairTasks_Required");
    }

    [Fact]
    public void Validate_WhenRequiredValuesAreProvided_ShouldSucceed()
    {
        var result = _validator.TestValidate(new UpdateWorkOrderRepairTasksCommand(Guid.NewGuid(), [Guid.NewGuid()]));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
