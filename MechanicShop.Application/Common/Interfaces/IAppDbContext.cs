using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.Branches;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.Identity;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Parts;
using MechanicShop.Domain.Workorders;
using MechanicShop.Domain.Workorders.Billing;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MechanicShop.Application.Common.Interfaces;

public interface IAppDbContext
{
    public DbSet<Branch> Branches { get; }
    public DbSet<EmployeeBranchMembership> EmployeeBranchMemberships { get; }
    public DbSet<MechanicShop.Domain.Customers.Customer> Customers { get; }

    public DbSet<Part> Parts { get; }
    public DbSet<RepairTask> RepairTasks { get; }
    public DbSet<Vehicle> Vehicles { get; }
    public DbSet<WorkOrder> WorkOrders { get; }
    public DbSet<Employee> Employees { get; }
    public DbSet<Invoice> Invoices { get; }
    public DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
}
