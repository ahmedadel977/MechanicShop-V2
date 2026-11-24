using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.Workorders;
using MechanicShop.Domain.Branches;
using MechanicShop.Tests.Common.Customers;
using MechanicShop.Tests.Common.Employees;
using MechanicShop.Tests.Common.RepaireTasks;
using MechanicShop.Tests.Common.WorkOrders;

namespace MechanicShop.Application.SubcutaneousTests.Common;

internal static class SubcutaneousTestData
{
    private static int _appointmentSequence;

    public static async Task<WorkOrder> CreateWorkOrderAsync(
        IAppDbContext context,
        DateTimeOffset? startAt = null,
        CancellationToken cancellationToken = default,
        bool includeLabor = true)
    {
        var customer = CustomerFactory.CreateCustomer().Value;
        Vehicle vehicle = customer.Vehicles.First();
        Employee? labor = includeLabor ? EmployeeFactory.CreateLabor().Value : null;
        RepairTask task = RepairTaskFactory.CreateRepairTask().Value;
        var start = startAt ?? DateTimeOffset.UtcNow.Date
            .AddDays(1_000 + Interlocked.Increment(ref _appointmentSequence))
            .AddHours(10);
        var workOrder = WorkOrder.Create(
            Guid.NewGuid(),
            MechanicShop.Tests.Common.Branches.TestBranchIds.Main,
            vehicle.Id,
            start,
            start.AddHours(1),
            labor?.Id,
            MechanicShop.Domain.Workorders.Enums.Spot.A,
            [task]).Value;

        vehicle.Customer = customer;
        workOrder.Vehicle = vehicle;
        workOrder.Labor = labor;
        await context.Customers.AddAsync(customer, cancellationToken);
        if (labor is not null)
        {
            await context.Employees.AddAsync(labor, cancellationToken);
            var membership = EmployeeBranchMembership.Create(
                labor.Id,
                MechanicShop.Tests.Common.Branches.TestBranchIds.Main,
                false).Value;
            await context.EmployeeBranchMemberships.AddAsync(membership, cancellationToken);
        }
        await context.RepairTasks.AddAsync(task, cancellationToken);
        await context.WorkOrders.AddAsync(workOrder, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return workOrder;
    }
}
