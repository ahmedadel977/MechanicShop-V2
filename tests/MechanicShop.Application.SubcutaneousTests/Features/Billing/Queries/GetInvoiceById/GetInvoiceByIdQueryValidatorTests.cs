using FluentValidation.TestHelper;
using MechanicShop.Application.Features.Billing.Queries.GetInvoiceById;

namespace MechanicShop.Application.SubcutaneousTests.Features.Billing.Queries.GetInvoiceById;

public class GetInvoiceByIdQueryValidatorTests
{
    private readonly GetInvoiceByIdQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenInvoiceIdIsEmpty_ShouldReportRequiredError()
    {
        var result = _validator.TestValidate(new GetInvoiceByIdQuery(Guid.Empty));
        result.ShouldHaveValidationErrorFor(query => query.InvoiceId)
            .WithErrorCode("InvoiceId_Is_Required");
    }

    [Fact]
    public void Validate_WhenInvoiceIdIsValid_ShouldSucceed()
    {
        var result = _validator.TestValidate(new GetInvoiceByIdQuery(Guid.NewGuid()));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
