using FluentValidation.TestHelper;
using MechanicShop.Application.Features.WorkOrders.common.AssignLabor;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.AssignLabor;

public class AssignLaborCommandValidatorTests
{
    private readonly AssignLaborCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenWorkOrderIdIsEmpty_ShouldReturnPropertyError()
    {
        var result = _validator.TestValidate(new AssignLaborCommand(Guid.Empty, Guid.NewGuid()));

        result.ShouldHaveValidationErrorFor(command => command.WorkOrderId)
            .WithErrorCode("WorkOrderId_Required");
    }

    [Fact]
    public void Validate_WhenLaborIdIsEmpty_ShouldReturnPropertyError()
    {
        var result = _validator.TestValidate(new AssignLaborCommand(Guid.NewGuid(), Guid.Empty));

        result.ShouldHaveValidationErrorFor(command => command.LaborId)
            .WithErrorCode("LaborId_Required");
    }

    [Fact]
    public void Validate_WhenBothIdsAreValid_ShouldSucceed()
    {
        var result = _validator.TestValidate(new AssignLaborCommand(Guid.NewGuid(), Guid.NewGuid()));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
