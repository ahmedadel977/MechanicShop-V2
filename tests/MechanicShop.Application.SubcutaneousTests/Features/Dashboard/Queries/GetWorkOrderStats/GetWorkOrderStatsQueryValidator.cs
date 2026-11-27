using FluentValidation.TestHelper;
using MechanicShop.Application.Features.Dashbord.Queris.GetWOkorderstats;

namespace MechanicShop.Application.SubcutaneousTests.Features.Dashboard.Queries.GetWorkOrderStats;

public class GetWorkOrderStatsQueryValidatorTests
{
    private readonly GetWorkorderStatsQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenFromUtcAndToUtcAreDefault_ShouldReportRequiredErrors()
    {
        var result = _validator.TestValidate(new GetWorkorderStatsQuery(default, default));

        result.ShouldHaveValidationErrorFor(query => query.FromUtc);
        result.ShouldHaveValidationErrorFor(query => query.ToUtc);
    }

    [Fact]
    public void Validate_WhenFromUtcIsAfterToUtc_ShouldReportError()
    {
        var result = _validator.TestValidate(new GetWorkorderStatsQuery(
            new DateTimeOffset(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero),
            new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero)));

        result.ShouldHaveValidationErrorFor(query => query.FromUtc);
    }

    [Fact]
    public void Validate_WhenFromUtcAndToUtcAreValid_ShouldSucceed()
    {
        var result = _validator.TestValidate(new GetWorkorderStatsQuery(
            new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero),
            new DateTimeOffset(new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), TimeSpan.Zero)));

        result.ShouldNotHaveAnyValidationErrors();
    }
}
