using FluentValidation.TestHelper;
using MechanicShop.Application.Features.Billing.Queries.GetInvoicePdf;

namespace MechanicShop.Application.SubcutaneousTests.Features.Billing.Queries.GetInvoicePdf;

public class GetInvoicePdfQueryValidatorTests
{
    private readonly GetInvoicePdfQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenInvoiceIdIsEmpty_ShouldReportRequiredError()
    {
        var result = _validator.TestValidate(new GetInvoicePdfQuery(Guid.Empty));
        result.ShouldHaveValidationErrorFor(query => query.InvoiceId)
            .WithErrorCode("InvoiceId_Is_Required");
    }

    [Fact]
    public void Validate_WhenInvoiceIdIsValid_ShouldSucceed()
    {
        var result = _validator.TestValidate(new GetInvoicePdfQuery(Guid.NewGuid()));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
