using MechanicShop.Api.IntegrationTests.Common;
using MechanicShop.Domain.Branches;
using MechanicShop.Infrastructure.Date;
using MechanicShop.Tests.Common.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MechanicShop.Api.IntegrationTests.Infrastructure;

[Collection(WebAppFactoryCollection.CollectionName)]
public sealed class DatabaseInitializationTests(WebAppFactory factory)
{
    [Fact]
    public async Task RepeatedSeeding_ShouldRemainDeterministicAndKeepBranchRelationshipsValid()
    {
        using var scope = factory.CreateScope();
        var initializer = scope.ServiceProvider.GetRequiredService<ApplicationDbContextInitialiser>();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cancellationToken = TestContext.Current.CancellationToken;

        await context.WorkOrders.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);

        try
        {
            await initializer.SeedAsync();
            await initializer.SeedAsync();

            var branches = await context.Branches.OrderBy(branch => branch.Code).ToListAsync(cancellationToken);
            Assert.Equal(2, branches.Count);
            Assert.Equal(TestBranchIds.Main, branches.Single(branch => branch.Code == "MAIN").Id);
            Assert.Equal(TestBranchIds.North, branches.Single(branch => branch.Code == "NORTH").Id);

            var memberships = await context.EmployeeBranchMemberships.ToListAsync(cancellationToken);
            Assert.Equal(10, memberships.Count);
            foreach (var employeeMemberships in memberships.GroupBy(membership => membership.EmployeeId))
            {
                Assert.Single(employeeMemberships, membership => membership.IsDefault);
                Assert.All(employeeMemberships, membership =>
                    Assert.Contains(branches, branch => branch.Id == membership.BranchId && branch.IsActive));
            }

            var workOrders = await context.WorkOrders.IgnoreQueryFilters().ToListAsync(cancellationToken);
            Assert.Equal(4, workOrders.Count);
            Assert.All(workOrders, workOrder =>
            {
                Assert.Contains(branches, branch => branch.Id == workOrder.BranchId);
                Assert.Contains(memberships, membership =>
                    membership.EmployeeId == workOrder.LaborId && membership.BranchId == workOrder.BranchId);
            });
        }
        finally
        {
            await context.WorkOrders.IgnoreQueryFilters().ExecuteDeleteAsync(cancellationToken);
        }
    }
}
