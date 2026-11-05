using MechanicShop.Application.Common.Interfaces;
using MechanicShop.Domain.Common;
using MechanicShop.Domain.Branches;
using MechanicShop.Domain.Customers;
using MechanicShop.Domain.Customers.Vehicles;
using MechanicShop.Domain.Employees;
using MechanicShop.Domain.Identity;
using MechanicShop.Domain.RepairTasks;
using MechanicShop.Domain.RepairTasks.Parts;
using MechanicShop.Domain.Workorders;
using MechanicShop.Domain.Workorders.Billing;
using MechanicShop.Infrastructure.identity;
using MediatR;

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace MechanicShop.Infrastructure.Date;

public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IMediator mediator,
    ICurrentBranchContext currentBranchContext) : IdentityDbContext<AppUser>(options), IAppDbContext
{
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<EmployeeBranchMembership> EmployeeBranchMemberships => Set<EmployeeBranchMembership>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Part> Parts => Set<Part>();
    public DbSet<RepairTask> RepairTasks => Set<RepairTask>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public Guid? CurrentBranchId => currentBranchContext.BranchId;
    public Guid? CurrentEmployeeId => currentBranchContext.EmployeeId;
    public bool IsCompanyWide => currentBranchContext.IsCompanyWide;
    public bool IsLabor => currentBranchContext.IsLabor;

    public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken)
        => Database.BeginTransactionAsync(cancellationToken);

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var domainEntities = ChangeTracker.Entries()
            .Where(entry => entry.Entity is Entity entity && entity.DomainEvents.Count != 0)
            .Select(entry => (Entity)entry.Entity)
            .ToList();
        var domainEvents = domainEntities.SelectMany(entity => entity.DomainEvents).ToList();

        int rowsAffected;
        try
        {
            rowsAffected = await base.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            foreach (var entity in domainEntities)
            {
                entity.ClearDomainEvents();
            }

            throw;
        }

        await DispatchDomainEventsAsync(domainEntities, domainEvents, cancellationToken);
        return rowsAffected;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        builder.Entity<WorkOrder>()
            .HasQueryFilter(workOrder =>
                (IsCompanyWide && Branches.Any(branch =>
                    branch.Id == workOrder.BranchId && branch.IsActive))
                || (!IsCompanyWide
                    && CurrentBranchId.HasValue
                    && workOrder.BranchId == CurrentBranchId.GetValueOrDefault()));
        builder.Entity<Invoice>()
            .HasQueryFilter(invoice => invoice.WorkOrder != null
                && ((IsCompanyWide && Branches.Any(branch =>
                        branch.Id == invoice.WorkOrder.BranchId && branch.IsActive))
                    || (!IsCompanyWide
                        && CurrentBranchId.HasValue
                        && invoice.WorkOrder.BranchId == CurrentBranchId.GetValueOrDefault())));
    }

    private async Task DispatchDomainEventsAsync(
        IReadOnlyCollection<Entity> domainEntities,
        IReadOnlyCollection<INotification> domainEvents,
        CancellationToken cancellationToken)
    {
        foreach (var domainEvent in domainEvents)
        {
            await mediator.Publish(domainEvent, cancellationToken);
        }

        foreach (var entity in domainEntities)
        {
            entity.ClearDomainEvents();
        }
    }
}
