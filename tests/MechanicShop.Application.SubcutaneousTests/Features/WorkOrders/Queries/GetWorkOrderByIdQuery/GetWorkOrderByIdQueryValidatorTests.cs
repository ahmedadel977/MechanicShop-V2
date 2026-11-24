using FluentValidation.TestHelper;
using MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrderByIdQuery;

namespace MechanicShop.Application.SubcutaneousTests.Features.WorkOrders.Queries.GetWorkOrderByIdQuery;

public class GetWorkOrderByIdQueryValidatorTests
{
    private readonly GetAppointmentByIdQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenWorkOrderIdIsEmpty_ShouldReportRequiredError()
    {
        var result = _validator.TestValidate(new MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrderByIdQuery.GetWorkOrderByIdQuery(Guid.Empty));
        result.ShouldHaveValidationErrorFor(query => query.WorkOrderId)
            .WithErrorCode("WorkOrderId_Is_Required");
    }

    [Fact]
    public void Validate_WhenWorkOrderIdIsValid_ShouldSucceed()
    {
        var result = _validator.TestValidate(new MechanicShop.Application.Features.WorkOrders.Queries.GetWorkOrderByIdQuery.GetWorkOrderByIdQuery(Guid.NewGuid()));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
