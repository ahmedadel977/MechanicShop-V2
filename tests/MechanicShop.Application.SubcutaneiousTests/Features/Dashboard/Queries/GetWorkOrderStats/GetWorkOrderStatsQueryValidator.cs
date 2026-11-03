using FluentValidation.TestHelper;
using MechanicShop.Application.Features.Dashbord.Queris.GetWOkorderstats;

namespace MechanicShop.Application.SubcutaneousTests.Features.Dashboard.Queries.GetWorkOrderStats;

public class GetWorkOrderStatsQueryValidatorTests
{
    private readonly GetWorkorderStatsQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenDateIsDefault_ShouldReportRequiredError()
    {
        var result = _validator.TestValidate(new GetWorkorderStatsQuery(default));
        result.ShouldHaveValidationErrorFor(query => query.Date);
    }

    [Fact]
    public void Validate_WhenDateIsSet_ShouldSucceed()
    {
        var result = _validator.TestValidate(new GetWorkorderStatsQuery(new DateOnly(2026, 1, 1)));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
