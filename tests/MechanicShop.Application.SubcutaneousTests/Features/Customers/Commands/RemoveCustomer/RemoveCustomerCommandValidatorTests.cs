using FluentValidation.TestHelper;
using MechanicShop.Application.Features.Customers.Commands.RemoveCustomer;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Commands.RemoveCustomer;

public class RemoveCustomerCommandValidatorTests
{
    private readonly RemoveCustomerCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCustomerIdIsEmpty_ShouldReportRequiredError()
    {
        var result = _validator.TestValidate(new RemoveCustomerCommand(Guid.Empty));
        result.ShouldHaveValidationErrorFor(command => command.CustomerId)
            .WithErrorMessage("Customer Id is required.");
    }

    [Fact]
    public void Validate_WhenCustomerIdIsValid_ShouldSucceed()
    {
        var result = _validator.TestValidate(new RemoveCustomerCommand(Guid.NewGuid()));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
