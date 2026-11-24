using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.Commands.CreateCustomer;
using MechanicShop.Application.SubcutaneousTests.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Commands.CreateCustomer;

[Collection(WebAppFactoryCollection.CollectionName)]
public class CreateCustomerCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenCommandIsValid_ShouldPersistCustomerAndVehicles()
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        var command = ValidCommand(email);

        var result = await _mediator.Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        var customer = await _context.Customers
            .Include(item => item.Vehicles)
            .SingleAsync(item => item.Email == email, TestContext.Current.CancellationToken);
        Assert.Equal(command.Name, customer.Name);
        Assert.Single(customer.Vehicles);
        Assert.Equal(command.Vehicles[0].LicensePlate, customer.Vehicles.First().LicensePlate);
        Assert.NotEqual(Guid.Empty, customer.Id);
    }

    [Fact]
    public async Task Handle_WhenEmailAlreadyExists_ShouldReturnErrorWithoutAddingCustomer()
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        var first = await _mediator.Send(ValidCommand(email), TestContext.Current.CancellationToken);
        Assert.False(first.IsError);

        var duplicate = await _mediator.Send(ValidCommand(email.ToUpperInvariant()), TestContext.Current.CancellationToken);

        Assert.True(duplicate.IsError);
        Assert.Equal(1, await _context.Customers.CountAsync(
            customer => customer.Email == email,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WhenCustomerHasMultipleVehicles_ShouldPersistEveryVehicle()
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        var command = ValidCommand(email) with
        {
            Vehicles =
            [
                new CreateVehicleCommand("Toyota", "Corolla", 2020, $"A-{Guid.NewGuid():N}"[..8]),
                new CreateVehicleCommand("Honda", "Civic", 2022, $"B-{Guid.NewGuid():N}"[..8])
            ]
        };

        var result = await _mediator.Send(command, TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        var persisted = await _context.Customers.AsNoTracking()
            .Include(customer => customer.Vehicles)
            .SingleAsync(customer => customer.Email == email, TestContext.Current.CancellationToken);
        Assert.Equal(2, persisted.Vehicles.Count());
        Assert.Equal(
            command.Vehicles.Select(vehicle => vehicle.LicensePlate).Order(),
            persisted.Vehicles.Select(vehicle => vehicle.LicensePlate).Order());
    }

    private static CreateCustomerCommand ValidCommand(string email) => new(
        "Test Customer",
        "5555555555",
        email,
        [new CreateVehicleCommand("Toyota", "Corolla", 2020, $"TEST-{Guid.NewGuid():N}"[..10])]);
}
