
using MechanicShop.Application.Features.Customers.Mappers;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Tests.Common.Customers;

using Xunit;

namespace MechanicShop.Application.UnitTests.Mappers;

public class CustomerMapperTests
{
    // =================================
    // Customer ToDto Tests
    // =================================

    [Fact]
    public void ToDto_ShouldMapCustomerCorrectly()
    {
        // Arrange
        var customer = CustomerFactory.CreateCustomer().Value;
        var vehicles = customer.Vehicles.ToList();

        // Act
        var dto = customer.ToDto();

        // Assert
        Assert.Equal(customer.Id, dto.CustomerId);
        Assert.Equal(customer.Name, dto.Name);
        Assert.Equal(customer.Email, dto.Email);
        Assert.Equal(customer.PhoneNumber, dto.PhoneNumber);

        Assert.Equal(vehicles.Count, dto.Vehicles.Count);

        for (var i = 0; i < vehicles.Count; i++)
        {
            Assert.Equal(vehicles[i].Id, dto.Vehicles[i].VehicleId);
            Assert.Equal(vehicles[i].Make, dto.Vehicles[i].Make);
            Assert.Equal(vehicles[i].Model, dto.Vehicles[i].Model);
            Assert.Equal(vehicles[i].Year, dto.Vehicles[i].Year);
            Assert.Equal(vehicles[i].LicensePlate, dto.Vehicles[i].LicensePlate);
        }
    }

    [Fact]
    public void ToDto_WithNullCustomer_ShouldThrowArgumentNullException()
    {
        // Arrange
        Customer? customer = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => customer!.ToDto());
    }


    // =================================
    // Customer ToDtos Tests
    // =================================

    [Fact]
    public void ToDtos_WithEmptyCollection_ShouldReturnEmptyList()
    {
        // Arrange
        var customers = new List<Customer>();

        // Act
        var result = customers.ToDtos();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void ToDtos_WithMultipleCustomers_ShouldMapAllCustomers()
    {
        // Arrange
        var customer1 = CustomerFactory.CreateCustomer().Value;
        var customer2 = CustomerFactory.CreateCustomer().Value;

        var customers = new List<Customer>
        {
            customer1,
            customer2
        };

        // Act
        var result = customers.ToDtos();

        // Assert
        Assert.Equal(2, result.Count);

        Assert.Equal(customer1.Id, result[0].CustomerId);
        Assert.Equal(customer1.Name, result[0].Name);

        Assert.Equal(customer2.Id, result[1].CustomerId);
        Assert.Equal(customer2.Name, result[1].Name);
    }


    // =================================
    // Vehicle ToDto Tests
    // =================================

    [Fact]
    public void VehicleToDto_ShouldMapVehicleCorrectly()
    {
        // Arrange
        var customer = CustomerFactory.CreateCustomer().Value;
        var vehicle = customer.Vehicles.First();

        // Act
        var dto = vehicle.ToDto();

        // Assert
        Assert.Equal(vehicle.Id, dto.VehicleId);
        Assert.Equal(vehicle.Make, dto.Make);
        Assert.Equal(vehicle.Model, dto.Model);
        Assert.Equal(vehicle.Year, dto.Year);
        Assert.Equal(vehicle.LicensePlate, dto.LicensePlate);
    }

    [Fact]
    public void VehicleToDto_WithNullVehicle_ShouldThrowArgumentNullException()
    {
        // Arrange
        Vehicle? vehicle = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => vehicle!.ToDto());
    }


    // =================================
    // Vehicle ToDtos Tests
    // =================================

    [Fact]
    public void VehicleToDtos_WithEmptyCollection_ShouldReturnEmptyList()
    {
        // Arrange
        var vehicles = new List<Vehicle>();

        // Act
        var result = vehicles.ToDtos();

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void VehicleToDtos_WithMultipleVehicles_ShouldMapAllVehicles()
    {
        // Arrange
        var customer1 = CustomerFactory.CreateCustomer().Value;
        var customer2 = CustomerFactory.CreateCustomer().Value;

        var vehicles = customer1.Vehicles
            .Concat(customer2.Vehicles)
            .ToList();

        // Act
        var result = vehicles.ToDtos();

        // Assert
        Assert.Equal(vehicles.Count, result.Count);

        for (var i = 0; i < vehicles.Count; i++)
        {
            Assert.Equal(vehicles[i].Id, result[i].VehicleId);
            Assert.Equal(vehicles[i].Make, result[i].Make);
            Assert.Equal(vehicles[i].Model, result[i].Model);
            Assert.Equal(vehicles[i].Year, result[i].Year);
            Assert.Equal(vehicles[i].LicensePlate, result[i].LicensePlate);
        }
    }
}

