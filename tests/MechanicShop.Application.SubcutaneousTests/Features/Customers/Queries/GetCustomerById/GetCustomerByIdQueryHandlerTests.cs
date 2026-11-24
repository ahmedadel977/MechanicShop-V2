using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.Queries.GetCustomerbyid;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Tests.Common.Customers;
using MediatR;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Queries.GetCustomerById;

[Collection(WebAppFactoryCollection.CollectionName)]
public class GetCustomerByIdQueryHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenCustomerDoesNotExist_ShouldReturnNotFound()
    {
        var result = await _mediator.Send(
            new GetCustomerByIdQuery(Guid.NewGuid()),
            TestContext.Current.CancellationToken);
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Handle_WhenCustomerExists_ShouldReturnVehicles()
    {
        var customer = CustomerFactory.CreateCustomer().Value;
        await _context.Customers.AddAsync(customer, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new GetCustomerByIdQuery(customer.Id),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.Equal(customer.Id, result.Value.CustomerId);
        Assert.Equal(customer.Vehicles.Count(), result.Value.Vehicles.Count);
    }
}
