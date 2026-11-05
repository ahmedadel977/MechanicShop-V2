using MechanicShop.Application.Features.Employees.Commands.CreateBranchManager;

namespace MechanicShop.Application.UniTests.Features.Employees;

public sealed class CreateBranchManagerCommandValidatorTests
{
    private readonly CreateBranchManagerCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_ShouldSucceed()
    {
        var result = _validator.Validate(new CreateBranchManagerCommand(
            "Ahmed", "Ali", "manager@example.com", "Password1", Guid.NewGuid()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WhenRequiredValuesAreMissing_ShouldFail()
    {
        var result = _validator.Validate(new CreateBranchManagerCommand(
            "", "", "invalid-email", "short", Guid.Empty));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateBranchManagerCommand.FirstName));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateBranchManagerCommand.LastName));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateBranchManagerCommand.Email));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateBranchManagerCommand.Password));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateBranchManagerCommand.BranchId));
    }
}
