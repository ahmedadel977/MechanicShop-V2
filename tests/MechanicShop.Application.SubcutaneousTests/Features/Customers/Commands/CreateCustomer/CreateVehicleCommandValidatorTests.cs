using FluentValidation.TestHelper;
using MechanicShop.Application.Features.Customers.Commands.CreateCustomer;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Commands.CreateCustomer;

public class CreateVehicleCommandValidatorTests
{
    private readonly CreateVehicleCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenVehicleFieldsAreValid_ShouldSucceed()
    {
        var result = _validator.TestValidate(new CreateVehicleCommand("Toyota", "Corolla", 2020, "ABC123"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenMakeIsMissing_ShouldReportMakeError()
    {
        var result = _validator.TestValidate(new CreateVehicleCommand("", "Corolla", 2020, "ABC123"));
        result.ShouldHaveValidationErrorFor(vehicle => vehicle.Make);
    }

    [Fact]
    public void Validate_WhenLicensePlateExceedsMaximumLength_ShouldReportLicensePlateError()
    {
        var result = _validator.TestValidate(new CreateVehicleCommand("Toyota", "Corolla", 2020, "12345678901"));
        result.ShouldHaveValidationErrorFor(vehicle => vehicle.LicensePlate);
    }
}
