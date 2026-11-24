using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Customers.Commands.RemoveCustomer;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Tests.Common.Customers;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Application.SubcutaneousTests.Features.Customers.Commands.RemoveCustomer;

[Collection(WebAppFactoryCollection.CollectionName)]
public class RemoveCustomerCommandHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_WhenCustomerDoesNotExist_ShouldReturnNotFound()
    {
        var result = await _mediator.Send(
            new RemoveCustomerCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Handle_WhenCustomerHasNoWorkOrders_ShouldDeleteCustomer()
    {
        var customer = CustomerFactory.CreateCustomer().Value;
        await _context.Customers.AddAsync(customer, TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _mediator.Send(
            new RemoveCustomerCommand(customer.Id),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        Assert.False(await _context.Customers.AnyAsync(
            item => item.Id == customer.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WhenCustomerHasAWorkOrder_ShouldPreventDeletion()
    {
        var order = await SubcutaneousTestData.CreateWorkOrderAsync(
            _context,
            cancellationToken: TestContext.Current.CancellationToken);
        var customerId = order.Vehicle!.Customer!.Id;

        var result = await _mediator.Send(
            new RemoveCustomerCommand(customerId),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.True(await _context.Customers.AnyAsync(
            customer => customer.Id == customerId,
            TestContext.Current.CancellationToken));
    }
}
