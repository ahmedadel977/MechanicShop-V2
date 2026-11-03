using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.Commands.UpdateCustomer;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Tests.Common.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Commands.UpdateCustomer;

[Collection(WebAppFactoryCollection.CollectionName)]
public class UpdateCustomerCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenCustomerDoesNotExist_ShouldReturnError()
    {
        var result = await _mediator.Send(
            new UpdateCustomerCommand(Guid.NewGuid(), "Valid", "5555555555", "valid@example.test", [
                new UpdateVehicleCommand(null, "Toyota", "Corolla", 2020, "TEST1234")]),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Handle_WhenCustomerAndVehicleAreValid_ShouldPersistUpdates()
    {
        var customer = CustomerFactory.CreateCustomer().Value;
        var vehicle = customer.Vehicles.First();
        await _context.Customers.AddAsync(customer, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var command = new UpdateCustomerCommand(
            customer.Id,
            "Updated Customer",
            "5555555556",
            $"{Guid.NewGuid():N}@example.test",
            [new UpdateVehicleCommand(vehicle.Id, "Honda", "Civic", 2022, "UPDATED1")]);

        var result = await _mediator.Send(command, TestContext.Current.CancellationToken);

        var persisted = await _context.Customers.AsNoTracking()
            .Include(item => item.Vehicles)
            .SingleAsync(item => item.Id == customer.Id, TestContext.Current.CancellationToken);
        Assert.False(result.IsError);
        Assert.Equal(command.Name, persisted.Name);
        Assert.Equal(command.Email, persisted.Email);
        var updatedVehicle = Assert.Single(persisted.Vehicles, item => item.Id == vehicle.Id);
        Assert.Equal("Honda", updatedVehicle.Make);
        Assert.Equal("Civic", updatedVehicle.Model);
    }

    [Fact]
    public async Task Handle_WhenVehicleBelongsToAnotherCustomer_ShouldRejectUpdateAndPreserveBothCustomers()
    {
        var owner = CustomerFactory.CreateCustomer(email: $"owner-{Guid.NewGuid():N}@example.test").Value;
        var otherCustomer = CustomerFactory.CreateCustomer(email: $"other-{Guid.NewGuid():N}@example.test").Value;
        var foreignVehicle = otherCustomer.Vehicles.First();
        await _context.Customers.AddRangeAsync(owner, otherCustomer);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new UpdateCustomerCommand(
                owner.Id,
                "Attempted Update",
                "5555555556",
                $"updated-{Guid.NewGuid():N}@example.test",
                [new UpdateVehicleCommand(foreignVehicle.Id, "Honda", "Civic", 2022, "FOREIGN1")]),
            TestContext.Current.CancellationToken);

        var persistedOwner = await _context.Customers.AsNoTracking()
            .Include(item => item.Vehicles)
            .SingleAsync(item => item.Id == owner.Id, TestContext.Current.CancellationToken);
        var persistedForeignVehicle = await _context.Vehicles.AsNoTracking()
            .SingleAsync(item => item.Id == foreignVehicle.Id, TestContext.Current.CancellationToken);
        Assert.True(result.IsError);
        Assert.Equal("Customer.Vehicle.Ownership.Invalid", result.TopError.Code);
        Assert.Equal(owner.Name, persistedOwner.Name);
        Assert.DoesNotContain(persistedOwner.Vehicles, item => item.Id == foreignVehicle.Id);
        Assert.Equal(otherCustomer.Id, persistedForeignVehicle.CustomerId);
    }

}
