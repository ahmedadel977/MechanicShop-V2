using FluentValidation.TestHelper;
using MechanicShop.Application.Features.Billing.Commands.SettleInvoice;

namespace MechanicShop.Application.SubcutaneiousTests.Features.Billing.Queries;

public class SettleInvoiceCommandValidatorTests
{
    private readonly SettleInvoiceCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenInvoiceIdIsEmpty_ShouldReturnRequiredError()
    {
        var result = _validator.TestValidate(new SettleInvoiceCommand(Guid.Empty));

        result.ShouldHaveValidationErrorFor(command => command.InvoiceId)
            .WithErrorCode("InvoiceId_Is_Required")
            .WithErrorMessage("InvoiceId is required.");
    }

    [Fact]
    public void Validate_WhenInvoiceIdIsValid_ShouldSucceed()
    {
        var result = _validator.TestValidate(new SettleInvoiceCommand(Guid.NewGuid()));

        result.ShouldNotHaveValidationErrorFor(command => command.InvoiceId);
    }
}
