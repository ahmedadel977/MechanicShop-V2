using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Application.Features.Labors.Queries.GetLabors;
using MechanicShop.Application.SubcutaneousTests.Common;
using MechanicShop.Tests.Common.Employees;
using MediatR;

namespace MechanicShop.Application.SubcutaneousTests.Features.Labors.Queries.GetLabors;

[Collection(WebAppFactoryCollection.CollectionName)]
public class GetLaborsQueryHandlerTests(WebAppFactory factory)
{
    private readonly IMediator _mediator = factory.CreateMediator();
    private readonly IAppDbContext _context = factory.CreateAppDbContext();

    [Fact]
    public async Task Handle_ShouldIncludeLaborEmployees()
    {
        var labor = EmployeeFactory.CreateLabor().Value;
        await _context.Employees.AddAsync(labor, TestContext.Current.CancellationToken);
        await _context.EmployeeBranchMemberships.AddAsync(
            MechanicShop.Domain.Branches.EmployeeBranchMembership.Create(
                labor.Id, MechanicShop.Tests.Common.Branches.TestBranchIds.Main, false).Value,
            TestContext.Current.CancellationToken);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _mediator.Send(new GetLaborsQuery(), TestContext.Current.CancellationToken);

        Assert.False(result.IsError);
        var dto = Assert.Single(result.Value, item => item.LaborId == labor.Id);
        Assert.False(string.IsNullOrWhiteSpace(dto.Name));
    }
}
