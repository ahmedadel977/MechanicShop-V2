using FluentValidation.TestHelper;
using MechanicShop.Application.Features.Customers.Commands.UpdateCustomer;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Commands.UpdateCustomer;

public class UpdateCustomerCommandValidatorTests
{
    private readonly UpdateCustomerCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_ShouldSucceed()
    {
        var result = _validator.TestValidate(ValidCommand());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenCustomerIdIsEmpty_ShouldReportIdError()
    {
        var result = _validator.TestValidate(ValidCommand() with { CustomerId = Guid.Empty });
        result.ShouldHaveValidationErrorFor(command => command.CustomerId);
    }

    [Fact]
    public void Validate_WhenPhoneNumberHasLetters_ShouldReportPhoneError()
    {
        var result = _validator.TestValidate(ValidCommand() with { PhoneNumber = "555-ABC" });
        result.ShouldHaveValidationErrorFor(command => command.PhoneNumber);
    }

    [Fact]
    public void Validate_WhenVehicleCollectionIsEmpty_ShouldRequireAtLeastOneVehicle()
    {
        var result = _validator.TestValidate(ValidCommand() with { Vehicles = [] });

        result.ShouldHaveValidationErrorFor(command => command.Vehicles)
            .WithErrorMessage("At least one vehicle is required.");
    }

    private static UpdateCustomerCommand ValidCommand() => new(
        Guid.NewGuid(),
        "Customer",
        "5555555555",
        "customer@example.test",
        [new UpdateVehicleCommand(null, "Toyota", "Corolla", 2020, "ABC123")]);
}
