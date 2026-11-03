using MechanicShop.Application.Features.Customers.Commands.CreateCustomer;
using MechanicShop.Application.Features.Customers.Queries.GetCustomers;
using MechanicShop.Application.SubcutaneousTests.Common;
using MediatR;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Queries.GetCustomers;

[Collection(WebAppFactoryCollection.CollectionName)]
public class GetCustomersQueryHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();

    [Fact]
    public async Task Handle_ShouldReturnCustomerAndItsVehicle()
    {
        var email = $"{Guid.NewGuid():N}@example.test";
        var create = new CreateCustomerCommand(
            "Query Customer",
            "5555555555",
            email,
            [new CreateVehicleCommand("Toyota", "Corolla", 2020, $"Q{Guid.NewGuid():N}"[..10])]);
        var created = await _mediator.Send(create, TestContext.Current.CancellationToken);
        Assert.False(created.IsError);

        var result = await _mediator.Send(new GetCustomersQuery(), TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        var customer = Assert.Single(result.Value, item => item.Email == email);
        Assert.Equal("Query Customer", customer.Name);
        Assert.Single(customer.Vehicles);
    }
}
