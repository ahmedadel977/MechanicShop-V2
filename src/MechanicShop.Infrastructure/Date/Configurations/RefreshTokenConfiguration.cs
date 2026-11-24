using MechanicShop.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MechanicShop.Infrastructure.Date.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(rt => rt.Id).IsClustered(false);

        builder.Property(rt => rt.TokenHash).HasMaxLength(64).IsRequired();

        builder.HasIndex(rt => rt.TokenHash).IsUnique();

        builder.Property(rt => rt.UserId).IsRequired();
        builder.Property(rt => rt.FamilyId).IsRequired();

        builder.Property(rt => rt.ExpiresOnUtc).IsRequired();
        builder.Property(rt => rt.RevokedOnUtc);
        builder.Property(rt => rt.ReplacedByTokenHash).HasMaxLength(64);
        builder.HasIndex(rt => new { rt.UserId, rt.FamilyId });
    }
}
