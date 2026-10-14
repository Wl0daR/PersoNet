using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PersoNet.Domain.Organization;
using PersoNet.Domain.Tenants;

namespace PersoNet.Infrastructure.Persistence.Configurations;

internal sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.HasKey(position => position.Id);
        builder.Property(position => position.Id).ValueGeneratedNever();

        builder.Property(position => position.Name).HasMaxLength(Position.NameMaxLength).IsRequired();
        builder.Property(position => position.NormalizedName).HasMaxLength(Position.NameMaxLength).IsRequired();
        builder.HasIndex(position => new { position.TenantId, position.NormalizedName }).IsUnique();
        builder.Property(position => position.DepartmentIds).IsRequired();

        builder.HasOne<Tenant>().WithMany().HasForeignKey(position => position.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}
