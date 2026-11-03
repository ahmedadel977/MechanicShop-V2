using MechanicShop.Application.Features.Labors.Mappers;
using MechanicShop.Domain.Employees;
using MechanicShop.Tests.Common.Employees;

using Xunit;

namespace MechanicShop.Application.UnitTests.Mappers;

public class LaborMapperTests
{
    // =================================
    // Employee ToDto Tests
    // =================================

    [Fact]
    public void ToDto_ShouldMapLaborCorrectly()
    {
        // Arrange
        var labor = EmployeeFactory.CreateLabor().Value;

        // Act
        var dto = labor.ToDto();

        // Assert
        Assert.Equal(labor.Id, dto.LaborId);
        Assert.Equal(labor.FullName, dto.Name);
    }


    // =================================
    // Employee ToDtos Tests
    // =================================

    [Fact]
    public void ToDtos_WithEmptyCollection_ShouldReturnEmptyList()
    {
        // Arrange
        var employees = new List<Employee>();

        // Act
        var result = employees.ToDtos();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ToDtos_WithMultipleEmployees_ShouldMapAllEmployees()
    {
        // Arrange
        var labor1 = EmployeeFactory.CreateLabor().Value;
        var labor2 = EmployeeFactory.CreateLabor().Value;

        var employees = new List<Employee>
        {
            labor1,
            labor2
        };

        // Act
        var result = employees.ToDtos();

        // Assert
        Assert.Equal(2, result.Count);

        Assert.Equal(labor1.Id, result[0].LaborId);
        Assert.Equal(labor1.FullName, result[0].Name);

        Assert.Equal(labor2.Id, result[1].LaborId);
        Assert.Equal(labor2.FullName, result[1].Name);
    }
}