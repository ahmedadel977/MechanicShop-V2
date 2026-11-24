using FluentValidation.TestHelper;
using MechanicShop.Application.Features.Customers.Commands.CreateCustomer;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Commands.CreateCustomer;

public class CreateCustomerCommandValidatorTests
{
    private readonly CreateCustomerCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenAllFieldsAreValid_ShouldSucceed()
    {
        var result = _validator.TestValidate(ValidCommand());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenEmailIsInvalid_ShouldReportEmailError()
    {
        var command = ValidCommand() with { Email = "not-an-email" };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(item => item.Email);
    }

    [Fact]
    public void Validate_WhenVehicleListIsEmpty_ShouldRequireAtLeastOneVehicle()
    {
        var command = ValidCommand() with { Vehicles = [] };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(item => item.Vehicles)
            .WithErrorMessage("At least one vehicle is required.");
    }

    private static CreateCustomerCommand ValidCommand() => new(
        "Valid Customer",
        "5555555555",
        "valid@example.test",
        [new CreateVehicleCommand("Toyota", "Corolla", 2020, "TEST1234")]);
}
