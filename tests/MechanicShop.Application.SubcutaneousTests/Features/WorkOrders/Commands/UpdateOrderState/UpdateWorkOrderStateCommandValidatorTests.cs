using FluentValidation.TestHelper;
using MechanicShop.Application.Features.WorkOrders.common.UpdateOrderState;
using MechanicShop.Domain.Workorders.Enums;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.UpdateOrderState;

public class UpdateWorkOrderStateCommandValidatorTests
{
    private readonly UpdateWorkOrderStateCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenStateIsUndefined_ShouldReturnError()
    {
        var result = _validator.TestValidate(new UpdateWorkOrderStateCommand(Guid.NewGuid(), (WorkOrderState)999));

        result.ShouldHaveValidationErrorFor(command => command.State)
            .WithErrorCode("WorkOrderStatus_Invalid");
    }

    [Fact]
    public void Validate_WhenStateIsDefined_ShouldSucceed()
    {
        var result = _validator.TestValidate(new UpdateWorkOrderStateCommand(Guid.Empty, WorkOrderState.Scheduled));

        result.ShouldNotHaveValidationErrorFor(command => command.State);
    }
}
