using MechanicShop.Domain.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Date.Configurations;

public sealed class EmployeeBranchMembershipConfiguration : IEntityTypeConfiguration<EmployeeBranchMembership>
{
    public void Configure(EntityTypeBuilder<EmployeeBranchMembership> builder)
    {
        builder.HasKey(membership => new { membership.EmployeeId, membership.BranchId });
        builder.HasOne<MechanicShop.Domain.Employees.Employee>()
            .WithMany()
            .HasForeignKey(membership => membership.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<MechanicShop.Domain.Branches.Branch>()
            .WithMany()
            .HasForeignKey(membership => membership.BranchId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(membership => membership.EmployeeId)
            .IsUnique()
            .HasFilter("[IsDefault] = 1");
        builder.Property(membership => membership.IsDefault).IsRequired();
    }
}
