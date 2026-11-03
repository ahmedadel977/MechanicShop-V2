
using MechanicShop.Application.Features.RepairTasks.Mappers;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Parts;
using MechanicShop.Tests.Common.RepaireTasks;

using Xunit;

namespace MechanicShop.Application.UnitTests.Mappers;

public class RepairTaskMapperTests
{
    // =================================
    // RepairTask ToDto Tests
    // =================================

    [Fact]
    public void ToDto_ShouldMapRepairTaskCorrectly()
    {
        // Arrange
        var part1 = PartFactory.CreatePart(
            cost: 100m,
            quantity: 2).Value;

        var part2 = PartFactory.CreatePart(
            cost: 50m,
            quantity: 3).Value;

        var repairTask = RepairTaskFactory.CreateRepairTask(
            name: "Engine Repair",
            laborCost: 200m,
            parts: [part1, part2]).Value;

        // Act
        var dto = repairTask.ToDto();

        // Assert
        Assert.Equal(repairTask.Id, dto.RepairTaskId);
        Assert.Equal(repairTask.Name, dto.Name);
        Assert.Equal(repairTask.LaborCost, dto.LaborCost);
        Assert.Equal(repairTask.TotalCost, dto.TotalCost);
        Assert.Equal(
            repairTask.EstimatedDurationInMins,
            dto.EstimatedDurationInMins);

        Assert.Equal(repairTask.Parts.Count(), dto.Parts.Count);

        for (var i = 0; i < repairTask.Parts.Count(); i++)
        {
            var part = repairTask.Parts.ElementAt(i);
            var partDto = dto.Parts[i];

            Assert.Equal(part.Id, partDto.PartId);
            Assert.Equal(part.Name, partDto.Name);
            Assert.Equal(part.Cost, partDto.Cost);
            Assert.Equal(part.Quantity, partDto.Quantity);
        }
    }

    [Fact]
    public void ToDto_WithNullRepairTask_ShouldThrowArgumentNullException()
    {
        // Arrange
        RepairTask? repairTask = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => repairTask!.ToDto());
    }


    // =================================
    // RepairTask ToDtos Tests
    // =================================

    [Fact]
    public void ToDtos_WithEmptyCollection_ShouldReturnEmptyList()
    {
        // Arrange
        var repairTasks = new List<RepairTask>();

        // Act
        var result = repairTasks.ToDtos();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ToDtos_WithMultipleRepairTasks_ShouldMapAll()
    {
        // Arrange
        var repairTask1 = RepairTaskFactory.CreateRepairTask(
            name: "Oil Change").Value;

        var repairTask2 = RepairTaskFactory.CreateRepairTask(
            name: "Engine Repair").Value;

        var repairTasks = new List<RepairTask>
        {
            repairTask1,
            repairTask2
        };

        // Act
        var result = repairTasks.ToDtos();

        // Assert
        Assert.Equal(2, result.Count);

        Assert.Equal(repairTask1.Id, result[0].RepairTaskId);
        Assert.Equal(repairTask1.Name, result[0].Name);

        Assert.Equal(repairTask2.Id, result[1].RepairTaskId);
        Assert.Equal(repairTask2.Name, result[1].Name);
    }


    // =================================
    // Part ToDto Tests
    // =================================

    [Fact]
    public void PartToDto_ShouldMapPartCorrectly()
    {
        // Arrange
        var part = PartFactory.CreatePart(
            cost: 100m,
            quantity: 2).Value;

        // Act
        var dto = part.ToDto();

        // Assert
        Assert.Equal(part.Id, dto.PartId);
        Assert.Equal(part.Name, dto.Name);
        Assert.Equal(part.Cost, dto.Cost);
        Assert.Equal(part.Quantity, dto.Quantity);
    }

    [Fact]
    public void PartToDto_WithNullPart_ShouldThrowArgumentNullException()
    {
        // Arrange
        Part? part = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => part!.ToDto());
    }


    // =================================
    // Part ToDtos Tests
    // =================================

    [Fact]
    public void PartToDtos_WithEmptyCollection_ShouldReturnEmptyList()
    {
        // Arrange
        var parts = new List<Part>();

        // Act
        var result = parts.ToDtos();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void PartToDtos_WithMultipleParts_ShouldMapAll()
    {
        // Arrange
        var part1 = PartFactory.CreatePart(
            cost: 100m,
            quantity: 2).Value;

        var part2 = PartFactory.CreatePart(
            cost: 50m,
            quantity: 3).Value;

        var parts = new List<Part>
        {
            part1,
            part2
        };

        // Act
        var result = parts.ToDtos();

        // Assert
        Assert.Equal(2, result.Count);

        for (var i = 0; i < parts.Count; i++)
        {
            Assert.Equal(parts[i].Id, result[i].PartId);
            Assert.Equal(parts[i].Name, result[i].Name);
            Assert.Equal(parts[i].Cost, result[i].Cost);
            Assert.Equal(parts[i].Quantity, result[i].Quantity);
        }
    }
}

