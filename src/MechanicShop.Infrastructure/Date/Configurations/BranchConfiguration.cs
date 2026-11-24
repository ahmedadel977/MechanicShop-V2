using MechanicShop.Domain.Branches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Date.Configurations;

public sealed class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.HasKey(branch => branch.Id);
        builder.Property(branch => branch.Name).HasMaxLength(120).IsRequired();
        builder.Property(branch => branch.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(branch => branch.Code).IsUnique();
        builder.Property(branch => branch.IsActive).IsRequired();
    }
}
