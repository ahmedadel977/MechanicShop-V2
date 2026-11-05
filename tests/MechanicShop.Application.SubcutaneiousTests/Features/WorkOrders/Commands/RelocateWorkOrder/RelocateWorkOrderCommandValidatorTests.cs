using FluentValidation.TestHelper;
using MechanicShop.Application.Features.WorkOrders.common.RelocateWorkOrder;
using MechanicShop.Domain.Workorders.Enums;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Commands.RelocateWorkOrder;

public class RelocateWorkOrderCommandValidatorTests
{
    private readonly RescheduleAppointmentCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenWorkOrderIdIsEmpty_ShouldReportError()
    {
        var result = _validator.TestValidate(new RelocateWorkOrderCommand(Guid.Empty, DateTimeOffset.UtcNow.AddDays(1), Spot.A));

        result.ShouldHaveValidationErrorFor(command => command.WorkOrderId);
    }

    [Fact]
    public void Validate_WhenNewStartIsInThePast_ShouldReportError()
    {
        var result = _validator.TestValidate(new RelocateWorkOrderCommand(Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), Spot.A));

        result.ShouldHaveValidationErrorFor(command => command.NewStartAt)
            .WithErrorMessage("New start time must be in the future.");
    }

    [Fact]
    public void Validate_WhenNewSpotIsUndefined_ShouldReportError()
    {
        var result = _validator.TestValidate(new RelocateWorkOrderCommand(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1), (Spot)999));

        result.ShouldHaveValidationErrorFor(command => command.NewSpot);
    }

    [Fact]
    public void Validate_WhenCommandIsValid_ShouldSucceed()
    {
        var result = _validator.TestValidate(new RelocateWorkOrderCommand(Guid.NewGuid(), DateTimeOffset.UtcNow.AddDays(1), Spot.A, new byte[8]));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
