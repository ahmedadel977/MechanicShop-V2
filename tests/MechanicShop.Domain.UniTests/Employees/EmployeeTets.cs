using MechanicShop.Domain.Common.Results;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.Identity;
using MechanicShop.Tests.Common.Employees;
using Xunit;

namespace MechanicShop.Domain.UnitTests.Employees;


public class EmployeeTests
{
    [Fact]
    public void CreateEmployee_ShouldSucceed_WithValidData()
    {
        // Arrange
        var id = Guid.NewGuid();
        const string firstName = "Ahmed";
        const string lastName = "Adel";
        const Role role = Role.Labor;

        // Act
        var result = EmployeeFactory.CreateEmployee(
            id: id,
            firstName: firstName,
            lastName: lastName,
            role: role);

        // Assert
        Assert.True(result.IsSuccess);

        var employee = result.Value;

        Assert.Equal(id, employee.Id);
        Assert.Equal(firstName, employee.FirstName);
        Assert.Equal(lastName, employee.LastName);
        Assert.Equal(role, employee.Role);
        Assert.Equal("Ahmed Adel", employee.FullName);
    }

    [Fact]
    public void CreateEmployee_ShouldFail_WhenIdIsEmpty()
    {
        // Arrange & Act
        var result = EmployeeFactory.CreateEmployee(id: Guid.Empty);

        // Assert
        Assert.True(result.IsError);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateEmployee_ShouldFail_WhenFirstNameIsInvalid(
        string firstName)
    {
        // Arrange & Act
        var result = EmployeeFactory.CreateEmployee(
            firstName: firstName);

        // Assert
        Assert.True(result.IsError);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateEmployee_ShouldFail_WhenLastNameIsInvalid(
        string lastName)
    {
        // Arrange & Act
        var result = EmployeeFactory.CreateEmployee(
            lastName: lastName);

        // Assert
        Assert.True(result.IsError);
    }

    [Fact]
    public void CreateEmployee_ShouldTrimFirstNameAndLastName()
    {
        // Arrange & Act
        var result = EmployeeFactory.CreateEmployee(
            firstName: "  Ahmed  ",
            lastName: "  Adel  ");

        // Assert
        Assert.True(result.IsSuccess);

        var employee = result.Value;

        Assert.Equal("Ahmed", employee.FirstName);
        Assert.Equal("Adel", employee.LastName);
        Assert.Equal("Ahmed Adel", employee.FullName);
    }

    [Fact]
    public void CreateEmployee_ShouldFail_WhenRoleIsInvalid()
    {
        // Arrange
        var invalidRole = (Role)999;

        // Act
        var result = EmployeeFactory.CreateEmployee(role: invalidRole);

        // Assert
        Assert.True(result.IsError);
    }

    [Fact]
    public void CreateLabor_ShouldSucceed_WithLaborRole()
    {
        // Arrange & Act
        var result = EmployeeFactory.CreateLabor();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(Role.Labor, result.Value.Role);
    }

    [Fact]
    public void CreateManager_ShouldSucceed_WithManagerRole()
    {
        // Arrange & Act
        var result = EmployeeFactory.CreateManager();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(Role.Manager, result.Value.Role);
    }

    [Fact]
    public void CreateEmployee_ShouldDefaultToBranchScope()
    {
        var employee = Employee.Create(Guid.NewGuid(), "Mona", "Ali", Role.Manager).Value;

        Assert.Equal(AccessScope.Branch, employee.AccessScope);
    }

    [Fact]
    public void CreateEmployee_ShouldAllowCompanyScopeForManager()
    {
        var employee = Employee.Create(
            Guid.NewGuid(), "Mona", "Ali", Role.Manager, AccessScope.Company).Value;

        Assert.Equal(AccessScope.Company, employee.AccessScope);
    }

    [Fact]
    public void CreateEmployee_ShouldRejectCompanyScopeForLabor()
    {
        var result = Employee.Create(
            Guid.NewGuid(), "Mona", "Ali", Role.Labor, AccessScope.Company);

        Assert.True(result.IsError);
        Assert.Equal("Employee.AccessScope.Invalid", result.TopError.Code);
    }

    [Fact]
    public void CreateEmployee_ShouldRejectUndefinedAccessScope()
    {
        var result = Employee.Create(
            Guid.NewGuid(), "Mona", "Ali", Role.Manager, (AccessScope)999);

        Assert.True(result.IsError);
        Assert.Equal("Employee.AccessScope.Invalid", result.TopError.Code);
    }

    [Fact]
    public void SetAccessScope_ShouldAllowCompanyScopeForManager()
    {
        var employee = Employee.Create(Guid.NewGuid(), "Mona", "Ali", Role.Manager).Value;

        var result = employee.SetAccessScope(AccessScope.Company);

        Assert.False(result.IsError);
        Assert.Equal(AccessScope.Company, employee.AccessScope);
    }

    [Fact]
    public void SetAccessScope_ShouldRejectCompanyScopeForLabor()
    {
        var employee = Employee.Create(Guid.NewGuid(), "Mona", "Ali", Role.Labor).Value;

        var result = employee.SetAccessScope(AccessScope.Company);

        Assert.True(result.IsError);
        Assert.Equal(AccessScope.Branch, employee.AccessScope);
    }
}
