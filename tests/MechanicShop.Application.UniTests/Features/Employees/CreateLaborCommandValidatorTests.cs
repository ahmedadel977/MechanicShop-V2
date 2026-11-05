using MechanicShop.Application.Features.Employees.Commands.CreateLabor;

namespace MechanicShop.Application.UniTests.Features.Employees;

public sealed class CreateLaborCommandValidatorTests
{
    private readonly CreateLaborCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_ShouldSucceed()
    {
        var result = _validator.Validate(new CreateLaborCommand(
            "Ahmed", "Ali", "labor@example.com", "Password1", Guid.NewGuid()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenRequiredValuesAreMissing_ShouldFail()
    {
        var result = _validator.Validate(new CreateLaborCommand(
            "", "", "invalid-email", "short", Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateLaborCommand.FirstName));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateLaborCommand.LastName));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateLaborCommand.Email));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateLaborCommand.Password));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateLaborCommand.BranchId));
    }
}
