using FluentValidation.TestHelper;
using MechanicShop.Application.Features.Customers.Commands.UpdateCustomer;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Commands.UpdateCustomer;

public class UpdateVehicleCommandValidatorTests
{
    private readonly UpdateVehicleCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenVehicleValuesAreValid_ShouldSucceed()
    {
        var result = _validator.TestValidate(new UpdateVehicleCommand(null, "Toyota", "Corolla", 2020, "ABC123"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenModelIsMissing_ShouldReportModelError()
    {
        var result = _validator.TestValidate(new UpdateVehicleCommand(null, "Toyota", "", 2020, "ABC123"));
        result.ShouldHaveValidationErrorFor(vehicle => vehicle.Model);
    }

    [Fact]
    public void Validate_WhenLicensePlateExceedsMaximumLength_ShouldReportError()
    {
        var result = _validator.TestValidate(new UpdateVehicleCommand(null, "Toyota", "Corolla", 2020, "12345678901"));
        result.ShouldHaveValidationErrorFor(vehicle => vehicle.LicensePlate);
    }
}
