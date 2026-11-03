using FluentValidation.TestHelper;
using MechanicShop.Application.Features.Customers.Queries.GetCustomerbyid;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Queries.GetCustomerById;

public class GetCustomerByIdQueryValidatorTests
{
    private readonly GetCustomerByIdQueryvalidator _validator = new();

    [Fact]
    public void Validate_WhenCustomerIdIsEmpty_ShouldReportRequiredError()
    {
        var result = _validator.TestValidate(new GetCustomerByIdQuery(Guid.Empty));
        result.ShouldHaveValidationErrorFor(query => query.CustomerId)
            .WithErrorMessage("CustomerId is required.");
    }

    [Fact]
    public void Validate_WhenCustomerIdIsValid_ShouldSucceed()
    {
        var result = _validator.TestValidate(new GetCustomerByIdQuery(Guid.NewGuid()));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
