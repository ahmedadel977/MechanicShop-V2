using MechanicShop.Domain.Employees;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Date.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.FirstName)
               .IsRequired()
               .HasMaxLength(50);

        builder.Property(e => e.LastName)
               .IsRequired()
               .HasMaxLength(50);

        builder.Property(e => e.Role).HasConversion<string>().IsRequired();
        builder.Property(e => e.AccessScope).HasConversion<string>().HasMaxLength(20).IsRequired();
    }
}
