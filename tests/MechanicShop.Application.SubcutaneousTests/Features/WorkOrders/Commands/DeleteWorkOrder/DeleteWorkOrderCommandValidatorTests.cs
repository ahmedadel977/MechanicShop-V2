using FluentValidation.TestHelper;
using MechanicShop.Application.Features.WorkOrders.common.DeleteWorkOrder;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.DeleteWorkOrder;

public class DeleteWorkOrderCommandValidatorTests
{
    private readonly DeleteWorkOrderCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenWorkOrderIdIsEmpty_ShouldReportRequiredError()
    {
        var result = _validator.TestValidate(new DeleteWorkOrderCommand(Guid.Empty));

        result.ShouldHaveValidationErrorFor(command => command.WorkOrderId)
            .WithErrorCode("WorkOrderId_Required");
    }

    [Fact]
    public void Validate_WhenWorkOrderIdIsValid_ShouldSucceed()
    {
        var result = _validator.TestValidate(new DeleteWorkOrderCommand(Guid.NewGuid(), new byte[8]));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
