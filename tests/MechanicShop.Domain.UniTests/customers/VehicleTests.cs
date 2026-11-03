using MechanicShop.Domain.Common.Results;
using MechanicShop.Tests.Common.Customers;

namespace MechanicShop.Domain.UnitTests.Customers.Vehicles;

public class VehicleTests
{
    [Fact]
    public void CreateVehicle_ShouldSucceed_WithValidData()
    {
        // Arrange
        var id = Guid.NewGuid();
        const string make = "Toyota";
        const string model = "Corolla";
        const int year = 2024;
        const string licensePlate = "ABC 123";

        // Act
        var result = VehicleFactory.CreateVehicle(
            id: id,
            make: make,
            model: model,
            year: year,
            licensePlate: licensePlate);

        // Assert
        Assert.True(result.IsSuccess);

        var vehicle = result.Value;

        Assert.Equal(id, vehicle.Id);
        Assert.Equal(make, vehicle.Make);
        Assert.Equal(model, vehicle.Model);
        Assert.Equal(year, vehicle.Year);
        Assert.Equal(licensePlate, vehicle.LicensePlate);
    }

    [Fact]
    public void CreateVehicle_ShouldSucceed_WithDefaultValues()
    {
        // Act
        var result = VehicleFactory.CreateVehicle();

        // Assert
        Assert.True(result.IsSuccess);

        var vehicle = result.Value;

        Assert.Equal("Honda", vehicle.Make);
        Assert.Equal("Accord", vehicle.Model);
        Assert.Equal(2024, vehicle.Year);
        Assert.Equal("ABC 123", vehicle.LicensePlate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateVehicle_ShouldFail_WhenMakeIsInvalid(string make)
    {
        // Act
        var result = VehicleFactory.CreateVehicle(make: make);

        // Assert
        Assert.True(result.IsError);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateVehicle_ShouldFail_WhenModelIsInvalid(string model)
    {
        // Act
        var result = VehicleFactory.CreateVehicle(model: model);

        // Assert
        Assert.True(result.IsError);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateVehicle_ShouldFail_WhenLicensePlateIsInvalid(
        string licensePlate)
    {
        // Act
        var result = VehicleFactory.CreateVehicle(
            licensePlate: licensePlate);

        // Assert
        Assert.True(result.IsError);
    }

    [Theory]
    [InlineData(1885)]
    [InlineData(3000)]
    public void CreateVehicle_ShouldFail_WhenYearIsInvalid(int year)
    {
        // Act
        var result = VehicleFactory.CreateVehicle(year: year);

        // Assert
        Assert.True(result.IsError);
    }

    [Fact]
    public void UpdateVehicle_ShouldSucceed_WithValidData()
    {
        // Arrange
        var vehicle = VehicleFactory.CreateVehicle().Value;

        // Act
        var result = vehicle.Update(
            "Toyota",
            "Corolla",
            2023,
            "XYZ 789");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(Result.Updated, result.Value);

        Assert.Equal("Toyota", vehicle.Make);
        Assert.Equal("Corolla", vehicle.Model);
        Assert.Equal(2023, vehicle.Year);
        Assert.Equal("XYZ 789", vehicle.LicensePlate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateVehicle_ShouldFail_WhenMakeIsInvalid(string make)
    {
        // Arrange
        var vehicle = VehicleFactory.CreateVehicle().Value;

        // Act
        var result = vehicle.Update(
            make,
            "Corolla",
            2023,
            "XYZ 789");

        // Assert
        Assert.True(result.IsError);

        // Verify that the vehicle was not modified.
        Assert.Equal("Honda", vehicle.Make);
        Assert.Equal("Accord", vehicle.Model);
        Assert.Equal(2024, vehicle.Year);
        Assert.Equal("ABC 123", vehicle.LicensePlate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateVehicle_ShouldFail_WhenModelIsInvalid(string model)
    {
        // Arrange
        var vehicle = VehicleFactory.CreateVehicle().Value;

        // Act
        var result = vehicle.Update(
            "Toyota",
            model,
            2023,
            "XYZ 789");

        // Assert
        Assert.True(result.IsError);
    }

    [Theory]
    [InlineData(1885)]
    [InlineData(3000)]
    public void UpdateVehicle_ShouldFail_WhenYearIsInvalid(int year)
    {
        // Arrange
        var vehicle = VehicleFactory.CreateVehicle().Value;

        // Act
        var result = vehicle.Update(
            "Toyota",
            "Corolla",
            year,
            "XYZ 789");

        // Assert
        Assert.True(result.IsError);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateVehicle_ShouldFail_WhenLicensePlateIsInvalid(
        string licensePlate)
    {
        // Arrange
        var vehicle = VehicleFactory.CreateVehicle().Value;

        // Act
        var result = vehicle.Update(
            "Toyota",
            "Corolla",
            2023,
            licensePlate);

        // Assert
        Assert.True(result.IsError);
    }

    [Fact]
    public void VehicleInfo_ShouldReturnFormattedVehicleInformation()
    {
        // Arrange
        var vehicle = VehicleFactory.CreateVehicle(
            make: "Toyota",
            model: "Corolla",
            year: 2024).Value;

        // Act
        var actual = vehicle.VehicleInfo;

        // Assert
        Assert.Equal("Toyota | Corolla | 2024", actual);
    }

}