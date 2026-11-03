using FluentValidation.TestHelper;
using MechanicShop.Application.Features.Identity.Queries.GenerateTokens;

namespace MechanicShop.Application.SubcutaneousTests.Features.Identity.Queries.GenerateTokens;

public class GenerateTokenQueryValidatorsTests
{
    private readonly GenerateTokenQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenEmailIsMalformed_ShouldReportEmailError()
    {
        var result = _validator.TestValidate(new GenerateTokenQuery("invalid", "secret"));
        result.ShouldHaveValidationErrorFor(query => query.Email);
    }

    [Fact]
    public void Validate_WhenPasswordIsEmpty_ShouldReportRequiredError()
    {
        var result = _validator.TestValidate(new GenerateTokenQuery("valid@example.test", ""));
        result.ShouldHaveValidationErrorFor(query => query.Password)
            .WithErrorCode("Password_Null_Or_Empty");
    }

    [Fact]
    public void Validate_WhenCredentialsHaveValidShapes_ShouldSucceed()
    {
        var result = _validator.TestValidate(new GenerateTokenQuery("valid@example.test", "secret"));
        result.ShouldNotHaveAnyValidationErrors();
    }
}
